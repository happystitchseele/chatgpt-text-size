param([switch]$Disable, [string]$AppId)
$ErrorActionPreference = 'Stop'
$taskExe = Join-Path $PSScriptRoot 'ChatGPTTextSize.exe'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = 'ChatGPTTextSize-OnLaunch-' + $sid.Split('-')[-1]
if ($Disable) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Get-Process ChatGPTTextSize -ErrorAction SilentlyContinue | Where-Object Path -eq $taskExe | Stop-Process
    return
}
if (-not (Test-Path -LiteralPath $taskExe)) { throw 'Build ChatGPTTextSize.exe first: .\build.ps1' }
if (-not $AppId) {
    $apps = @(Get-StartApps | Where-Object Name -eq 'ChatGPT')
    if ($apps.Count -ne 1) { throw 'Cannot identify one ChatGPT Store app. Supply -AppId with its Get-StartApps AppID.' }
    $AppId = $apps[0].AppID
}
if ($AppId -notmatch '^OpenAI\.[A-Za-z0-9_.]+![A-Za-z0-9_.]+$') { throw 'Expected an OpenAI Store application ID.' }
$eventLog = 'Microsoft-Windows-AppModel-Runtime/Admin'
if (-not (Get-WinEvent -ListLog $eventLog).IsEnabled) { throw 'AppModel-Runtime/Admin event log must already be enabled.' }
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$task = $scheduler.NewTask(0)
$task.RegistrationInfo.Description = 'ChatGPT 启动事件触发 Ctrl+滚轮字号助手，无登录自启，无托盘图标。'
$task.Principal.UserId = $sid
$task.Principal.LogonType = 3
$task.Principal.RunLevel = 0
$task.Settings.Hidden = $true
$task.Settings.MultipleInstances = 2
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.ExecutionTimeLimit = 'PT0S'
$trigger = $task.Triggers.Create(0)
$trigger.Subscription = "<QueryList><Query Id='0' Path='$eventLog'><Select Path='$eventLog'>*[System[EventID=201]] and *[EventData[Data[@Name='ApplicationName']='$AppId']]</Select></Query></QueryList>"
$action = $task.Actions.Create(0)
$action.Path = $taskExe
$action.WorkingDirectory = $PSScriptRoot
[void]$scheduler.GetFolder('\').RegisterTaskDefinition($taskName,$task,6,$sid,$null,3,$null)
$oldStartup = Join-Path ([Environment]::GetFolderPath('Startup')) 'ChatGPT字号助手.lnk'
if (Test-Path -LiteralPath $oldStartup) {
    $oldLink = (New-Object -ComObject WScript.Shell).CreateShortcut($oldStartup)
    if ($oldLink.TargetPath -eq $taskExe) { Remove-Item -LiteralPath $oldStartup }
}
Write-Output "Enabled: $taskName ($AppId)"
