#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Alice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BattleHistoryApiClientTests {
    GameObject owner;
    HttpListener server;
    BattleHistoryApiClient client;

    [SetUp]
    public void SetUp() {
        var portFinder = new TcpListener(IPAddress.Loopback, 0);
        portFinder.Start();
        var port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
        portFinder.Stop();
        var baseUrl = $"http://127.0.0.1:{port}";
        server = new HttpListener();
        server.Prefixes.Add(baseUrl + "/");
        server.Start();
        owner = new GameObject("API test settings");
        var setting = owner.AddComponent<AppNetworkSetting>();
        typeof(AppNetworkSetting).GetField("cloudApiBaseUrl", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(setting, baseUrl + "/beat-striker/");
        client = new BattleHistoryApiClient(setting);
    }

    [TearDown]
    public void TearDown() {
        server?.Close();
        if (owner) UnityEngine.Object.DestroyImmediate(owner);
    }

    [UnityTest]
    public IEnumerator SavePostsUtf8JsonToPhpQueryRoute() {
        var served = RespondOnce("{\"ok\":true,\"id\":\"saved-record\"}");
        var save = client.SaveAsync(new BattleHistorySaveRequest {
            playerNames = new[] { "ゲスト", "プレイヤー２" },
            stage = "Street", musicId = "music-test", appVersion = "1.0",
            replayPayload = new ReplayPayload { stage = "Street", musicId = "music-test" },
        });
        yield return Finish(save, served);
        Assert.That(save.Result.ok, Is.True);
        Assert.That(save.Result.id, Is.EqualTo("saved-record"));
        var request = served.Result;
        Assert.That(request.Method, Is.EqualTo("POST"));
        Assert.That(request.Path, Is.EqualTo("/beat-striker/index.php"));
        Assert.That(request.Route, Is.EqualTo("/battle-histories"));
        Assert.That(request.ContentType, Does.StartWith("application/json"));
        Assert.That(request.Accept, Is.EqualTo("application/json"));
        var posted = JsonUtility.FromJson<BattleHistorySaveRequest>(request.Body);
        Assert.That(posted.playerNames, Is.EqualTo(new[] { "ゲスト", "プレイヤー２" }));
        Assert.That(posted.replayPayload.musicId, Is.EqualTo("music-test"));
    }

    [UnityTest]
    public IEnumerator ListClampsLimitAndDetailPreservesReplayFields() {
        var served = RespondOnce("{\"items\":[{\"id\":\"record\",\"playerNames\":[\"ゲスト\",\"ゲスト\"],\"hasReplay\":true}]}");
        var list = client.GetSummariesAsync(999);
        yield return Finish(list, served);
        Assert.That(served.Result.Route, Is.EqualTo("/battle-histories"));
        Assert.That(served.Result.Limit, Is.EqualTo("100"));
        Assert.That(list.Result.Length, Is.EqualTo(1));
        Assert.That(list.Result[0].hasReplay, Is.True);

        served = RespondOnce("{\"id\":\"record\",\"replayPayload\":{\"schemaVersion\":1,\"stage\":\"Street\",\"musicId\":\"music-test\",\"strikerIds\":[0,1],\"rounds\":[{\"roundNumber\":1,\"preBeatStates\":[{\"playerId\":0,\"position\":{\"x\":1.25,\"y\":0,\"z\":-1}}]}]}}");
        var detail = client.GetDetailAsync("record");
        yield return Finish(detail, served);
        Assert.That(served.Result.Method, Is.EqualTo("GET"));
        Assert.That(served.Result.Path, Is.EqualTo("/beat-striker/index.php"));
        Assert.That(served.Result.Route, Is.EqualTo("/battle-histories/record"));
        Assert.That(detail.Result.replayPayload.strikerIds, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(detail.Result.replayPayload.rounds[0].preBeatStates[0].position.x, Is.EqualTo(1.25f));
    }

    [UnityTest]
    public IEnumerator HttpErrorAndHtmlInstallationPageAreReportedAsFailures() {
        var served = RespondOnce("{\"error\":\"storage error\"}", 500);
        var list = client.GetSummariesAsync(50);
        yield return WaitFor(list, served);
        Assert.That(list.IsFaulted, Is.True);
        Assert.That(list.Exception.GetBaseException().Message, Does.Contain("status=500"));

        served = RespondOnce("<html>Installation page</html>", 200, "text/html");
        list = client.GetSummariesAsync(50);
        yield return WaitFor(list, served);
        Assert.That(list.IsFaulted, Is.True);
        Assert.That(list.Exception.GetBaseException().Message, Does.Contain("did not return JSON"));
    }

    [UnityTest]
    public IEnumerator RouteCannotInjectAQueryAndEmptyIdIsRejected() {
        var served = RespondOnce("{\"id\":\"test\"}");
        var detail = client.GetDetailAsync("id&limit=999?test");
        yield return Finish(detail, served);
        Assert.That(served.Result.Route, Is.EqualTo("/battle-histories/id&limit=999?test"));
        Assert.That(served.Result.Limit, Is.Null);

        var empty = client.GetDetailAsync("");
        yield return WaitFor(empty);
        Assert.That(empty.Exception.GetBaseException(), Is.TypeOf<ArgumentException>());
    }

    Task<CapturedRequest> RespondOnce(string body, int status = 200, string contentType = "application/json") {
        return Task.Run(async () => {
            var context = await server.GetContextAsync();
            var request = context.Request;
            var captured = new CapturedRequest {
                Method = request.HttpMethod, Path = request.Url.AbsolutePath,
                Route = request.QueryString["route"], Limit = request.QueryString["limit"],
                ContentType = request.ContentType, Accept = request.Headers["Accept"],
            };
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8)) {
                captured.Body = await reader.ReadToEndAsync();
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType + "; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
            return captured;
        });
    }

    static IEnumerator WaitFor(params Task[] tasks) {
        var all = Task.WhenAll(tasks);
        var deadline = Time.realtimeSinceStartup + 10;
        while (!all.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(all.IsCompleted, Is.True, "Local API test timed out.");
        // Individual tests inspect expected failures after the tasks complete.
        _ = all.Exception;
    }

    static IEnumerator Finish(params Task[] tasks) {
        yield return WaitFor(tasks);
        foreach (var task in tasks) task.GetAwaiter().GetResult();
    }

    class CapturedRequest {
        public string Method, Path, Route, Limit, ContentType, Accept, Body;
    }
}
#endif
