[CmdletBinding()]
param(
    [string]$FtpHost = 'rintify.sakura.ne.jp',
    [string]$RemoteDirectory = '/www/beat-striker',
    [string]$ApiBaseUrl = 'https://rintify.sakura.ne.jp/beat-striker',
    [PSCredential]$Credential,
    [string]$EnvFile
)

$ErrorActionPreference = 'Stop'
if ($FtpHost -match '[/@:]' -or [string]::IsNullOrWhiteSpace($FtpHost)) {
    throw 'FtpHostにはFTPサーバー名だけを指定してください。'
}
if ($RemoteDirectory -notmatch '^/(?:[^/?#]+/)*[^/?#]+/?$' -or $RemoteDirectory.Split('/') -contains '..') {
    throw 'RemoteDirectoryにはFTPログイン後の絶対ディレクトリを指定してください。'
}
if ($EnvFile) {
    if ($Credential) { throw 'EnvFileとCredentialはどちらか一方を指定してください。' }
    $settings = @{}
    foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding utf8) {
        if ($line -match '^\s*(SAKURA_FTP_USERNAME|SAKURA_FTP_PASSWORD)\s*=(.*)$') {
            $settings[$Matches[1]] = $Matches[2].Trim()
        }
    }
    foreach ($key in 'SAKURA_FTP_USERNAME', 'SAKURA_FTP_PASSWORD') {
        if ([string]::IsNullOrEmpty($settings[$key])) { throw "EnvFileに$keyを設定してください。" }
    }
    $password = ConvertTo-SecureString $settings.SAKURA_FTP_PASSWORD -AsPlainText -Force
    $Credential = [PSCredential]::new($settings.SAKURA_FTP_USERNAME, $password)
} elseif (-not $Credential) {
    $Credential = Get-Credential -Message 'さくらのFTPアカウント（サーバーの初期ドメインのアカウント名とパスワード）'
}
if (-not $Credential) { throw 'FTP認証がキャンセルされました。' }

$curl = (Get-Command curl -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$login = '{0}:{1}' -f $Credential.UserName, $Credential.GetNetworkCredential().Password
$escapedLogin = $login.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n')
$curlConfig = 'user = "{0}"' -f $escapedLogin
$curlOptions = @('--disable', '--config', '-', '--ssl-reqd', '--silent', '--show-error', '--connect-timeout', '10', '--max-time', '60')
$remoteUrl = "ftp://$FtpHost$($RemoteDirectory.TrimEnd('/'))/"
$uploadName = "index-upload-$([guid]::NewGuid().ToString('N')).php"
$source = Join-Path $PSScriptRoot 'www/beat-striker/index.php'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'アップロードするindex.phpが見つかりません。' }
$curlConfig | & $curl @curlOptions --ftp-create-dirs --upload-file $source --quote "-RNFR $uploadName" --quote '-RNTO index.php' "$remoteUrl$uploadName"
if ($LASTEXITCODE -ne 0) {
    $uploadExit = $LASTEXITCODE
    $curlConfig | & $curl @curlOptions --head --quote "+DELE $uploadName" $remoteUrl 2>$null | Out-Null
    throw "FTPSアップロードに失敗しました (curl exit $uploadExit、転送用ファイル $uploadName)。"
}
Write-Host 'アップロード完了: index.php'

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
$feedbackUrl = "$healthUrl`?route=%2Ffeedback&name=APIProbe"
$feedback = Invoke-RestMethod -Uri $feedbackUrl -TimeoutSec 30
if ($feedback.ok -ne $true -or $feedback.submitted -isnot [bool]) {
    throw "感想受付APIの応答が不正です: $feedbackUrl"
}
$feedbackListUrl = "$healthUrl`?route=%2Ffeedback-list"
$feedbackList = Invoke-WebRequest -Uri $feedbackListUrl -TimeoutSec 30
if ($feedbackList.Content -notmatch '<h1>感想一覧</h1>') {
    throw "感想一覧の応答が不正です: $feedbackListUrl"
}
Write-Host "API稼働確認完了: $healthUrl"
