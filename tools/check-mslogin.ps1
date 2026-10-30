<#
.SYNOPSIS
    星玥（XingYue）正版登录自检脚本。
    检查 ClientId 配置、公共客户端流开关、本地登录缓存与日志，并联网探测微软端点。

.DESCRIPTION
    只读脚本，不会修改环境变量、注册表或任何文件。
    按顺序执行 6 项检查，最后给出「当前卡在哪一步」。

.PARAMETER ClientId
    显式指定要检查的 ClientId（默认从环境变量 / 源码默认值读取）。

.PARAMETER AppDir
    启动器所在目录（包含 XingYue Launcher.exe）。默认是脚本所在目录的上一级。

.PARAMETER SkipNetwork
    跳过联网探测（离线环境或不想发请求时使用）。

.EXAMPLE
    pwsh -File tools\check-mslogin.ps1

.EXAMPLE
    pwsh -File tools\check-mslogin.ps1 -ClientId 2dd10ca4-a46f-40f0-b4be-ddc10cc9ec27
#>
[CmdletBinding()]
param(
    [string]$ClientId,
    [string]$AppDir,
    [switch]$SkipNetwork
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# ---------- 输出辅助 ----------
$script:Issues = New-Object System.Collections.Generic.List[string]
function Section([string]$t) { Write-Host ''; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok([string]$t) { Write-Host "  [OK]   $t" -ForegroundColor Green }
function Warn([string]$t) { Write-Host "  [注意] $t" -ForegroundColor Yellow }
function Bad([string]$t) { Write-Host "  [问题] $t" -ForegroundColor Red; $script:Issues.Add($t) }
function Info([string]$t) { Write-Host "  .      $t" -ForegroundColor DarkGray }

$IsGuid = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'

Write-Host "星玥 正版登录自检  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White

# ---------- 1. ClientId 解析 ----------
Section '1. ClientId 解析'

$procVar    = $env:XINGYUE_MS_CLIENT_ID
$userVar    = [Environment]::GetEnvironmentVariable('XINGYUE_MS_CLIENT_ID', 'User')
$machineVar = [Environment]::GetEnvironmentVariable('XINGYUE_MS_CLIENT_ID', 'Machine')

$effective = $null
$source = $null
if ($ClientId)      { $effective = $ClientId.Trim();  $source = '参数 -ClientId' }
elseif ($procVar)   { $effective = $procVar.Trim();   $source = '当前进程环境变量' }
elseif ($userVar)   { $effective = $userVar.Trim();   $source = '用户环境变量（setx 写入）' }
elseif ($machineVar){ $effective = $machineVar.Trim();$source = '系统环境变量' }

Info "当前进程变量 : $(if ($procVar) { $procVar } else { '<空>' })"
Info "用户级变量   : $(if ($userVar) { $userVar } else { '<空>' })"
Info "系统级变量   : $(if ($machineVar) { $machineVar } else { '<空>' })"

# 源码里的兜底默认值
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $AppDir) { $AppDir = $repoRoot }
$secretFile = Join-Path $repoRoot 'XingYue\Modules\ModSecret.vb'
$srcDefault = $null
if (Test-Path $secretFile) {
    # 优先看公共 ClientId 常量（开源版默认值）
    $m = Select-String -Path $secretFile -Pattern 'OAuthClientIdPublic\s+As\s+String\s*=\s*"([^"]*)"' -AllMatches |
         Select-Object -First 1
    if ($m -and $m.Matches.Count -gt 0) {
        $srcDefault = $m.Matches[0].Groups[1].Value
        Info "源码默认值   : $srcDefault（公共 ClientId，走 live.com 旧版端点）"
    } else {
        $m = Select-String -Path $secretFile -Pattern 'OAuthClientId\s+As\s+String\s*=\s*If\([^,]+,\s*"([^"]*)"' -AllMatches |
             Select-Object -First 1
        if ($m -and $m.Matches.Count -gt 0) { $srcDefault = $m.Matches[0].Groups[1].Value }
        if ($srcDefault) { Info "源码默认值   : $srcDefault" }
        else             { Info "源码默认值   : <空字符串>（未内置 ClientId）" }
    }
}

if (-not $effective -and $srcDefault) { $effective = $srcDefault; $source = '源码默认值' }

if (-not $effective) {
    Bad '没有找到任何 ClientId：环境变量为空，源码默认值也为空。'
    Info '解决：setx XINGYUE_MS_CLIENT_ID "<你的client-id>"，然后完全退出启动器再打开。'
} else {
    Ok "生效的 ClientId = $effective（来源：$source）"
    if ($effective -match $IsGuid) { Ok '格式是合法 GUID' }
    elseif ($effective -eq '00000000402b5328') { Ok '这是微软 Xbox 应用的公共 ClientId（不是 GUID，正常）' }
    else { Bad "格式不像 GUID（应为 8-4-4-4-12），请检查是否复制到「对象 ID」或「租户 ID」" }
}

if ($userVar -and -not $procVar) {
    Warn '用户变量已写入，但当前会话读不到——说明本进程启动早于 setx。'
    Info '运行启动器的进程必须重新启动才能读到新变量。'
}

# ---------- 2. 启动器进程与目录 ----------
Section '2. 启动器进程'

$procs = @(Get-Process -Name 'XingYue' -ErrorAction SilentlyContinue)
if ($procs.Count -gt 0) {
    foreach ($p in $procs) {
        $start = try { $p.StartTime } catch { $null }
        if ($start) { Warn "XingYue Launcher.exe 正在运行（PID $($p.Id)，启动于 $($start.ToString('HH:mm:ss'))）" }
        else        { Warn "XingYue Launcher.exe 正在运行（PID $($p.Id)）" }
    }
    Info '改完环境变量后必须完全退出（含托盘图标）再启动，否则读到的还是旧值。'
} else {
    Ok '启动器当前未运行'
}

$exe = $null
foreach ($cand in @(
        (Join-Path $AppDir 'XingYue Launcher.exe'),
        (Join-Path $AppDir 'bin\XingYue Launcher.exe'),
        (Join-Path $AppDir 'XingYue\bin\XingYue Launcher.exe'))) {
    if (-not $exe -and $cand -and (Test-Path $cand)) { $exe = $cand }
}
$runDir = if ($exe) { Split-Path -Parent $exe } else { $AppDir }
if ($exe) {
    Ok "找到启动器：$exe"
    Info ("编译时间：{0}" -f (Get-Item $exe).LastWriteTime.ToString('yyyy-MM-dd HH:mm'))
} else {
    Warn "在 $AppDir 及常见子目录下没找到 XingYue Launcher.exe，后续日志检查可能定位不到"
}

# ---------- 3. 公共客户端流（联网探测） ----------
Section '3. 公共客户端流 / 应用类型'

if ($SkipNetwork) {
    Info '已指定 -SkipNetwork，跳过联网探测'
} elseif (-not $effective) {
    Info '没有 ClientId，跳过联网探测'
} else {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $deviceCodeUrl = 'https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode'
    $tokenUrl      = 'https://login.microsoftonline.com/consumers/oauth2/v2.0/token'

    $body = "client_id=$effective&tenant=/consumers&scope=XboxLive.signin%20offline_access"
    try {
        $r = Invoke-WebRequest -Uri $deviceCodeUrl -Method Post -Body $body `
             -ContentType 'application/x-www-form-urlencoded' -UseBasicParsing -TimeoutSec 20
        $j = $r.Content | ConvertFrom-Json
        Ok 'devicecode 端点接受该 ClientId（应用存在且支持个人账户）'
        Info "返回 user_code = $($j.user_code)，有效期 $($j.expires_in) 秒（这次探测产生的代码会自然过期，可忽略）"
    } catch {
        # PowerShell 5.1 会把错误响应体放在 ErrorDetails 里，部分情况下只能从 Response 流读
        $txt = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $txt = $_.ErrorDetails.Message }
        if (-not $txt) {
            try {
                $sr = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
                $txt = $sr.ReadToEnd()
            } catch { }
        }
        $code = ''
        if ($txt -match 'AADSTS\d+') { $code = $Matches[0] }

        if ($txt -match 'AADSTS70002\b' -or $txt -match "marked as 'mobile'") {
            Bad '未开启「允许公共客户端流」：应用被当成机密客户端，设备码流被拒绝（AADSTS70002，must be marked as mobile）'
            Info '路径：应用 → 身份验证 → 高级设置 → 允许公共客户端流 = 是 → 保存。'
        } elseif ($txt -match 'AADSTS700016') {
            Bad '应用不存在或不属于「个人 Microsoft 账户」类型（AADSTS700016）'
            Info '检查：ClientId 是否抄错；「受支持的帐户类型」是否选了「仅个人 Microsoft 账户」。'
        } elseif ($txt -match 'AADSTS7000218') {
            Bad '未开启「允许公共客户端流」（AADSTS7000218）'
            Info '路径：应用 → 身份验证 → 高级设置 → 允许公共客户端流 = 是 → 保存。'
        } elseif ($txt -match 'AADSTS') {
            Bad "devicecode 返回错误 $code"
            Info ("原始响应：" + $txt.Substring(0, [Math]::Min(400, $txt.Length)))
        } elseif ($txt) {
            Bad "devicecode 请求失败：$txt"
        } else {
            Warn "无法连接微软服务器（$($_.Exception.Message)）"
            Info '常见原因：网络需要代理/加速器，或系统时间偏差过大。'
        }
    }

    # 补充说明：审批与账号状态无法在未登录时探测
    Info 'Mojang 的 API 审批状态无法在未登录时探测。'
    Info '若上面全绿、登录仍报 403 Invalid app registration，就是审批没通过（https://aka.ms/mce-reviewappid）。'
}

# ---------- 4. 本地登录缓存 ----------
Section '4. 本地登录缓存（注册表）'

$regPath = 'HKCU:\Software\XingYue'
if (Test-Path $regPath) {
    $props = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
    $checks = @(
        @{ Name = 'CacheMsV2OAuthRefresh'; Label = '微软 RefreshToken' },
        @{ Name = 'CacheMsV2Name';         Label = '缓存的玩家名' },
        @{ Name = 'LoginMsJson';           Label = '已保存的微软账号列表' },
        @{ Name = 'LoginType';             Label = '上次使用的登录方式' }
    )
    foreach ($c in $checks) {
        $v = $props.($c.Name)
        if ($null -ne $v -and "$v" -ne '') { Ok "$($c.Label) 存在（$($c.Name)，值已加密，长度 $("$v".Length)）" }
        else { Info "$($c.Label) 为空（$($c.Name)）" }
    }
    Info '这些都是加密存储的，脚本无法解密，只能判断有无。'
} else {
    Info "注册表项 $regPath 不存在 —— 启动器还没在这个用户下运行过，或从未登录。"
}

# ---------- 5. 日志 ----------
Section '5. 日志检查'

$logCandidates = @(@(
    (Join-Path $runDir '星玥\Log1.txt'),
    (Join-Path $AppDir '星玥\Log1.txt'),
    (Join-Path $repoRoot 'XingYue\bin\星玥\Log1.txt'),
    (Join-Path $env:APPDATA '星玥\Log1.txt')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)

if (-not $logCandidates) {
    Warn "没找到 Log1.txt（找过 $runDir\星玥\ 等位置）"
    Info '日志一般在 XingYue Launcher.exe 同级的「星玥」目录里。'
} else {
    $log = $logCandidates[0]
    Ok "日志：$log（修改于 $((Get-Item $log).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))）"
    $tail = Get-Content -LiteralPath $log -Tail 4000 -ErrorAction SilentlyContinue

    $patterns = [ordered]@{
        '网页登录地址'          = '已拿到 devicecode（ClientId 生效）'
        '开始微软登录步骤 1/6'  = '登录流程已启动'
        'AADSTS7000218'         = '公共客户端流未开启'
        'AADSTS700016'          = 'ClientId 无效'
        'invalid_client'        = 'ClientId 无效'
        'Invalid app registration' = 'Mojang 审批未通过'
        '登录轮询失败'          = '轮询 token 失败'
        '微软登录结束'          = '登录曾成功'
    }
    $found = $false
    foreach ($k in $patterns.Keys) {
        $hit = $tail | Select-String -SimpleMatch $k | Select-Object -Last 1
        if ($hit) {
            $found = $true
            Info ("命中 [{0}] → {1}" -f $k, $patterns[$k])
            Info ("   " + $hit.Line.Trim())
        }
    }
    if (-not $found) { Info '最近 4000 行里没有正版登录相关记录 —— 可能还没点过「正版登录」。' }
}

# ---------- 6. 结论 ----------
Section '6. 结论'

if ($script:Issues.Count -eq 0) {
    Ok '本机这几项检查都通过，没有发现配置问题。'
    Info '如果登录仍然失败，剩下的最可能是 Mojang 的 API 审批（403 Invalid app registration），'
    Info '这一步只能在 https://aka.ms/mce-reviewappid 提交后等待，脚本无法探测。'
} else {
    Write-Host "  [问题] 发现 $($script:Issues.Count) 个问题，按下面的顺序处理：" -ForegroundColor Red
    $i = 1
    foreach ($x in $script:Issues) { Write-Host "    $i. $x" -ForegroundColor Red; $i++ }
}

Write-Host ''
Write-Host '提示：本脚本只读。要写入 ClientId 请自行执行：' -ForegroundColor DarkGray
Write-Host '  setx XINGYUE_MS_CLIENT_ID "<你的client-id>"' -ForegroundColor DarkGray
