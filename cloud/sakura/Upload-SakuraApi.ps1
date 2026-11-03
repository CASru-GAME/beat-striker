[CmdletBinding()]
param(
    [string]$FtpHost = 'rintify.sakura.ne.jp',
    [string]$RemoteDirectory = '/www/beat-striker',
    [string]$ApiBaseUrl = 'https://rintify.sakura.ne.jp/beat-striker',
    [PSCredential]$Credential
)

$ErrorActionPreference = 'Stop'
if ($FtpHost -match '[/@:]' -or [string]::IsNullOrWhiteSpace($FtpHost)) {
    throw 'FtpHostにはFTPサーバー名だけを指定してください。'
}
if ($RemoteDirectory -notmatch '^/(?:[^/?#]+/)*[^/?#]+/?$' -or $RemoteDirectory.Split('/') -contains '..') {
    throw 'RemoteDirectoryにはFTPログイン後の絶対ディレクトリを指定してください。'
}
if (-not $Credential) {
    $Credential = Get-Credential -Message 'さくらのFTPアカウント（サーバーの初期ドメインのアカウント名とパスワード）'
}
if (-not $Credential) { throw 'FTP認証がキャンセルされました。' }

function New-FtpsRequest([string]$RemotePath, [string]$Method) {
    $request = [System.Net.FtpWebRequest]::Create("ftp://$FtpHost$RemotePath")
    $request.Method = $Method
    $request.Credentials = $Credential.GetNetworkCredential()
    $request.EnableSsl = $true
    $request.UsePassive = $true
    $request.UseBinary = $true
    $request.KeepAlive = $false
    $request.Timeout = 30000
    $request.ReadWriteTimeout = 30000
    return $request
}

$remote = $RemoteDirectory.TrimEnd('/')
try {
    $mkdir = New-FtpsRequest $remote ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)
    $response = $mkdir.GetResponse()
    $response.Close()
} catch [System.Net.WebException] {
    $failure = $_.Exception.Response
    $alreadyExists = $failure -and $failure.StatusCode -eq [System.Net.FtpStatusCode]::ActionNotTakenFileUnavailable
    if ($failure) { $failure.Close() }
    if (-not $alreadyExists) { throw }
    # A 550 may mean the directory exists; the upload below must still succeed.
}

$source = Join-Path $PSScriptRoot 'www/beat-striker/index.php'
$bytes = [System.IO.File]::ReadAllBytes($source)
$upload = New-FtpsRequest "$remote/index.php" ([System.Net.WebRequestMethods+Ftp]::UploadFile)
$upload.ContentLength = $bytes.Length
$stream = $upload.GetRequestStream()
try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
$response = $upload.GetResponse()
try { Write-Host "アップロード完了: $($response.StatusDescription.Trim())" } finally { $response.Close() }

$healthUrl = "$($ApiBaseUrl.TrimEnd('/'))/index.php"
$health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 30
if (-not $health.ok -or $health.service -ne 'beat-striker-api' -or $health.storage -ne 'sqlite') {
    throw "APIの応答が不正です: $healthUrl"
}
$listUrl = "$healthUrl`?route=%2Fbattle-histories&limit=1"
$list = Invoke-RestMethod -Uri $listUrl -TimeoutSec 30
if (-not ($list.PSObject.Properties.Name -contains 'items')) {
    throw "対戦履歴一覧の応答が不正です: $listUrl"
}
Write-Host "API稼働確認完了: $healthUrl"
