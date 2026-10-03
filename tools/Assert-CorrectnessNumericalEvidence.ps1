param([Parameter(Mandatory = $true)][xml]$Evidence)
$ErrorActionPreference = 'Stop'

# Formula coverage alone cannot authorize a release with missing numerical input classes.
$root = $Evidence.correctnessEvidence
if ($root.scope -ne 'all-discovered-configurations') { throw 'Numerical release evidence must cover every discovered configuration.' }
$required = @(([string]$root.requiredNumericalFixtures).Split(',') | Where-Object { $_ })
$minimum = @('tiny', 'large-offset', 'large', 'alternating-scale', 'evicted-spike', 'zero',
    'negative', 'subnormal', 'overflow-adjacent', 'cancelled-spike', 'cascaded-cancellation',
    'volume-tiny', 'volume-subnormal', 'volume-overflow-adjacent', 'volume-evicted-spike', 'volume-alternating-scale',
    'mixed-ohlc-extremes', 'mixed-ohlc-subnormal')
if ($required.Count -ne @($required | Sort-Object -Unique).Count) { throw 'Duplicate required numerical fixture names.' }
foreach ($shape in $minimum) {
    if ($required -cnotcontains "xorshift32-v1/seed-244/$shape") { throw "Missing required numerical class: $shape." }
}
$cases = @($root.case)
if ($cases.Count -eq 0) { throw 'No numerical case evidence was supplied.' }
$incomplete = [System.Collections.Generic.List[string]]::new()
foreach ($case in $cases) {
    if ($case.passed -ne 'True') { throw "Failing case: $($case.name)." }
    foreach ($slot in @(([string]$case.overflowReferenceSlots).Split(',') | Where-Object { $_ -ne '' })) {
        $exercised = @($case.fixture | Where-Object {
            $_.outputOverflowSlot -ceq $slot -and $_.outputOverflowRejectionsChecked -ceq '2' -and
            $_.passed -ceq 'True' -and $_.completed -ceq 'True'
        })
        if ($exercised.Count -eq 0) { $incomplete.Add("$($case.name): overflow reference slot $slot was never exercised") }
    }
    $overflowNames = @($case.fixture | Where-Object {
        ([string]$_.outputOverflowRejectionsChecked) -notin @('', '0') -or
        [string]$_.outputOverflowBarIndex -or [string]$_.outputOverflowSlot -or [string]$_.outputOverflowSign
    } | ForEach-Object { [string]$_.name })
    foreach ($name in @(($required + $overflowNames) | Sort-Object -Unique)) {
        $matches = @($case.fixture | Where-Object { $_.name -ceq $name })
        if ($matches.Count -ne 1 -or $matches[0].passed -ne 'True' -or $matches[0].completed -ne 'True') {
            $incomplete.Add("$($case.name): $name")
            continue
        }
        $fixtureBars = 0L
        $fixtureValues = 0L
        $receipt = $matches[0]
        $minimumBars = if ($required -ccontains $name) { 2 } else { 1 }
        if (-not [long]::TryParse([string]$receipt.inputBars, [ref]$fixtureBars) -or $fixtureBars -lt $minimumBars -or
            -not [long]::TryParse([string]$receipt.valuesChecked, [ref]$fixtureValues) -or $fixtureValues -lt 0) {
            $incomplete.Add("$($case.name): $name has insufficient executed-value evidence")
            continue
        }
        $rejections = 0L
        $rawRejections = [string]$receipt.outputOverflowRejectionsChecked
        if ($rawRejections -and (-not [long]::TryParse($rawRejections, [ref]$rejections) -or $rejections -lt 0)) {
            $incomplete.Add("$($case.name): $name has malformed overflow evidence")
            continue
        }
        if ($rejections -eq 0) {
            if ($fixtureValues -lt $fixtureBars -or [string]$receipt.outputOverflowBarIndex -or
                [string]$receipt.outputOverflowSlot -or [string]$receipt.outputOverflowSign) {
                $incomplete.Add("$($case.name): $name has insufficient executed-value evidence")
            }
            continue
        }
        $overflowBar = 0L
        $overflowSlot = 0L
        $overflowSign = 0L
        $caseValues = 0L
        $declaredSlots = @(([string]$case.overflowReferenceSlots).Split(',') | Where-Object { $_ -ne '' })
        $trajectorySlots = @(([string]$case.independentTrajectorySlots).Split(',') | Where-Object { $_ -ne '' })
        if ($rejections -ne 2 -or
            -not [long]::TryParse([string]$receipt.outputOverflowBarIndex, [ref]$overflowBar) -or
            $overflowBar -lt 0 -or $overflowBar -ge $fixtureBars -or $fixtureValues -lt $overflowBar -or
            -not [long]::TryParse([string]$receipt.outputOverflowSlot, [ref]$overflowSlot) -or $overflowSlot -lt 0 -or
            $declaredSlots -cnotcontains $overflowSlot.ToString() -or $trajectorySlots -cnotcontains $overflowSlot.ToString() -or
            -not [long]::TryParse([string]$receipt.outputOverflowSign, [ref]$overflowSign) -or $overflowSign -notin @(-1L, 1L) -or
            -not [long]::TryParse([string]$case.values, [ref]$caseValues) -or $caseValues -le 0) {
            $incomplete.Add("$($case.name): $name has incomplete oracle-backed overflow evidence")
        }
    }
}
if ($incomplete.Count -gt 0) {
    throw "Missing or unsuccessful numerical fixtures ($($incomplete.Count)): $([string]::Join('; ', @($incomplete | Select-Object -First 12)))"
}
