param([string]$Runtime='C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\hosted-runs\43779540029\artifact\roi-inputs\common')
$ErrorActionPreference='Stop'
$tensorPath=Join-Path $Runtime 'AiDotNet.Tensors.dll'
$assembly=[System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($tensorPath)
$rows=@()
foreach($name in @('AiDotNet.Tensors.Helpers.CpuParallelSettings','AiDotNet.Tensors.Helpers.TensorPrimitivesCore','AiDotNet.Tensors.Operators.AddOperatorDouble','AiDotNet.Tensors.Operators.FloorOperatorDouble','AiDotNet.Tensors.Operators.SinOperatorDouble')) {
    $type=$assembly.GetType($name,$true)
    $methods=@($type.GetMethods([System.Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly') | Where-Object {$_.Name -match '^(Invoke|LightweightParallel)'} | ForEach-Object {$_.ToString()})
    $rows+=@{type=$name;methods=$methods}
}
$result=@{path=$tensorPath;sha256=(Get-FileHash -LiteralPath $tensorPath).Hash.ToLowerInvariant();assembly=$assembly.FullName;apis=$rows;diagnostic_only=$true}
$json=$result | ConvertTo-Json -Depth 6
$json | Set-Content -LiteralPath 'C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\pointwise-pinned-tensor-apis-20261116.json'
$json
