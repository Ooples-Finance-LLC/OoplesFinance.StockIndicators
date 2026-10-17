function Get-TimingSample {
    $allProcesses = @(Get-CimInstance Win32_Process)
    $tracked = @{}
    foreach ($entry in $allProcesses) {
        if ($entry.Name -match '^(dotnet|testhost|MSBuild|VBCSCompiler|csc|vbc)\.exe$') {
            $tracked[$entry.ProcessId] = $entry
        }
    }
    do {
        $added = $false
        foreach ($entry in $allProcesses) {
            if (!$tracked.ContainsKey($entry.ProcessId) -and $tracked.ContainsKey($entry.ParentProcessId)) {
                $tracked[$entry.ProcessId] = $entry
                $added = $true
            }
        }
    } while ($added)
    [pscustomobject]@{
        Time = [DateTime]::UtcNow.ToString('o')
        Processes = @($tracked.Values | Sort-Object ProcessId | ForEach-Object {
            [pscustomobject]@{
                ProcessId = $_.ProcessId
                ParentProcessId = $_.ParentProcessId
                CommandLine = $_.CommandLine
                CreationTime = $_.CreationDate.ToUniversalTime().ToString('o')
                CpuSeconds = ([double]$_.KernelModeTime + [double]$_.UserModeTime) / 10000000
            }
        })
    }
}

function Get-TimingActivity($Baseline, $Sample, [int]$OwnProcessId = -1) {
    # Bound combined background CPU to 1% of one core, with 0.1 seconds
    # of timer/counter allowance. Callers check consecutive and full windows.
    # Unknown identities fail closed; a PID alone is insufficient identity.
    $elapsed = ([DateTimeOffset]$Sample.Time - [DateTimeOffset]$Baseline.Time).TotalSeconds
    if ($elapsed -lt 0) { throw 'Non-monotonic activity samples' }
    $cpuLimit = 0.1 + 0.01 * $elapsed
    $combinedCpu = 0.0
    $owned = @{}
    if ($OwnProcessId -ge 0) { $owned[$OwnProcessId] = $true }
    do {
        $added = $false
        foreach ($entry in $Sample.Processes) {
            if (!$owned.ContainsKey([int]$entry.ProcessId) -and $owned.ContainsKey([int]$entry.ParentProcessId)) {
                $owned[[int]$entry.ProcessId] = $true
                $added = $true
            }
        }
    } while ($added)
    foreach ($entry in $Sample.Processes) {
        if ($owned.ContainsKey([int]$entry.ProcessId)) { continue }
        $prior = @($Baseline.Processes | Where-Object {
            $_.ProcessId -eq $entry.ProcessId -and $_.CreationTime -eq $entry.CreationTime -and
            $_.CommandLine -ceq $entry.CommandLine
        })
        if ($prior.Count -ne 1) { $entry; continue }
        $delta = $entry.CpuSeconds - $prior[0].CpuSeconds
        if ($delta -lt 0) { $entry; continue }
        $combinedCpu += $delta
    }
    if ($combinedCpu -gt $cpuLimit) { [pscustomobject]@{ CpuSeconds = $combinedCpu; Limit = $cpuLimit } }
}
