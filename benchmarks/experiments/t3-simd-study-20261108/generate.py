"""Extract the unchanged scalar comparator and generate two diagnostic kernels."""
import hashlib, json, subprocess, sys
from pathlib import Path

root=Path(__file__).parent
source='b2afb7edd136c5acb79ddaf98ebc44d64c63a685'
blob=subprocess.check_output(['git','show',source+':src/Helpers/CertifiedTillson.cs'],cwd=root)
text=blob.decode(); start=text.index('    internal readonly struct WordSmoother')
opening=text.index('{',start); depth=1; end=opening+1
while depth:
    depth+=(text[end]=='{')-(text[end]=='}');end+=1
scalar='namespace T3Study;\ninternal static class Baseline\n{\n'+text[start:end]+'\n}\n'
inline=scalar.replace('internal static class Baseline','internal static class BaselineInline').replace('        internal double SmoothSameBinade(', '        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n        internal double SmoothSameBinade(')
assert inline!=scalar
files={'Scalar.cs':scalar,'ScalarInline.cs':inline}
for bits,instruction in [(256,'Avx2'),(512,'Avx512F')]:
    files[f'Kernel{bits}.cs']='''using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace T3Study;
internal readonly struct KernelBITS(int period)
{
    private readonly ulong d = (ulong)period + 1;
    private readonly ulong reciprocal = (ulong)(((UInt128)1 << 64) / ((ulong)period + 1));
    private static readonly VectorBITS<ulong> Mask32 = VectorBITS.Create(0xffffffffUL);
    private static readonly VectorBITS<ulong> Fraction = VectorBITS.Create((1UL << 52) - 1);
    private static readonly VectorBITS<ulong> Hidden = VectorBITS.Create(1UL << 52);
    private static readonly VectorBITS<ulong> One = VectorBITS.Create(1UL);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VectorBITS<ulong> Mul32(VectorBITS<ulong> a, VectorBITS<ulong> b) =>
        INSTRUCTION.Multiply(a.AsUInt32(), b.AsUInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static VectorBITS<ulong> SmallProduct(VectorBITS<ulong> a, ulong b) =>
        Mul32(a, VectorBITS.Create(b)) + (Mul32(a >> 32, VectorBITS.Create(b)) << 32);

    // Four exact 32x32 products, with the complete middle carry retained.
    // This independently tested primitive also covers unrestricted 64-bit inputs.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static VectorBITS<ulong> HighProduct(VectorBITS<ulong> a, VectorBITS<ulong> b)
    {
        var lo = Mul32(a, b);
        var cross1 = Mul32(a, b >> 32);
        var cross2 = Mul32(a >> 32, b);
        var middle = (lo >> 32) + (cross1 & Mask32) + (cross2 & Mask32);
        return Mul32(a >> 32, b >> 32) + (cross1 >> 32) + (cross2 >> 32) + (middle >> 32);
    }

    // Same preconditions as the original scalar SmoothSameBinade, per lane:
    // period1..2047 and positive normal operands with identical exponent bits.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VectorBITS<ulong> Smooth(VectorBITS<ulong> prior, VectorBITS<ulong> input)
    {
        var a = (prior & Fraction) | Hidden;
        var b = (input & Fraction) | Hidden;
        var denominator = VectorBITS.Create(d);
        var numerator = SmallProduct(a, d - 2) + (b << 1);
        var quotient = HighProduct(numerator, VectorBITS.Create(reciprocal));
        var remainder = numerator - SmallProduct(quotient, d);
        var correction = VectorBITS.GreaterThanOrEqual(remainder, denominator);
        quotient += correction & One;
        remainder -= correction & denominator;
        var complement = denominator - remainder;
        var tieOdd = VectorBITS.Equals(remainder, complement) & VectorBITS.Equals(quotient & One, One);
        quotient += (VectorBITS.GreaterThan(remainder, complement) | tieOdd) & One;
        return (prior & ~Fraction) | (quotient & Fraction);
    }
}
'''.replace('BITS',str(bits)).replace('INSTRUCTION',instruction)
manifest=dict(baseline=source,original_helper_sha256=hashlib.sha256(blob).hexdigest(),extracted_word_sha256=hashlib.sha256(text[start:end].encode()).hexdigest(),files={n:hashlib.sha256(v.encode()).hexdigest() for n,v in files.items()},prototype_files={n:hashlib.sha256((root/n).read_bytes().replace(b'\r\n',b'\n')).hexdigest() for n in ['Program.cs','Study.csproj']},generator_sha256=hashlib.sha256(Path(__file__).read_bytes().replace(b'\r\n',b'\n')).hexdigest(),note='Diagnostic prototype only. Original scalar word implementation is copied verbatim; scalar-inline adds only an inlining annotation. No production code, runtime environment override or qualification plan is changed.')
files['source-manifest.json']=json.dumps(manifest,indent=2)+'\n'
for name,content in files.items():
    path=root/name; raw=content.encode()
    if '--check' in sys.argv: assert path.read_bytes().replace(b'\r\n',b'\n')==raw,name
    else:path.write_bytes(raw)
print(json.dumps(manifest))
