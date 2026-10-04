<#
.SYNOPSIS
    常駐している ImeCenterView のリソース使用量を、外から定期的に記録する（長時間テスト用）。

.DESCRIPTION
    ハンドル数・GDI オブジェクト数・USER オブジェクト数・プライベートメモリ・ワーキングセットを、
    %APPDATA%\ImeCenterView\logs\external-<日時>.csv に追記する。
    アプリには触れず、数値を読むだけなので、Release 版をそのまま計測できる。
    アプリが終了したとき、MaxHours を過ぎたとき、Samples 回記録したときに止まる。
    アプリを起動し直すと別のプロセスになるため、このスクリプトも実行し直すこと。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\watch-resources.ps1
#>
param(
    # 記録する間隔（秒）
    [int]$IntervalSeconds = 60,
    # この時間を過ぎたら止める（時間）
    [int]$MaxHours = 24,
    # この回数だけ記録したら止める。0 なら回数では止めない
    [int]$Samples = 0
)

# GetGuiResources は解放が必要なリソースを生成しない
Add-Type -Namespace Win32 -Name Gui -MemberDefinition '[DllImport("user32.dll")] public static extern uint GetGuiResources(System.IntPtr hProcess, uint uiFlags);'

$p = Get-Process ImeCenterView -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    "ImeCenterView が起動していません"
    exit 1
}

$dir = Join-Path $env:APPDATA "ImeCenterView\logs"
New-Item -ItemType Directory -Force $dir | Out-Null
$log = Join-Path $dir ("external-{0:yyyyMMdd-HHmmss}.csv" -f (Get-Date))
"time,pid,handles,gdi,user,privateMB,workingSetMB" | Out-File $log -Encoding utf8
"記録先: $log（PID $($p.Id)）"

$deadline = (Get-Date).AddHours($MaxHours)
$count = 0
while ((Get-Date) -lt $deadline) {
    $p.Refresh()
    if ($p.HasExited) {
        "ImeCenterView が終了したため、記録を止めました"
        break
    }

    # 0 = GR_GDIOBJECTS、1 = GR_USEROBJECTS
    $gdi = [Win32.Gui]::GetGuiResources($p.Handle, 0)
    $user = [Win32.Gui]::GetGuiResources($p.Handle, 1)
    $line = "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4},{5:F1},{6:F1}" -f (Get-Date), $p.Id, $p.HandleCount, $gdi, $user, ($p.PrivateMemorySize64 / 1MB), ($p.WorkingSet64 / 1MB)
    $line | Out-File $log -Append -Encoding utf8

    $count++
    if ($Samples -gt 0 -and $count -ge $Samples) {
        $line
        break
    }

    Start-Sleep -Seconds $IntervalSeconds
}
