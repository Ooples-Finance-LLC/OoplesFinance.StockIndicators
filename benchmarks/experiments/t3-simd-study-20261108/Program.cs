using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using T3Study;

internal static class Program
{
    private const ulong Fraction = (1UL << 52) - 1;
    private static ulong sink;
    private static long oracleLanes, productLanes, chainValues;
    private static readonly Vector512<ulong> ShiftIndices = Vector512.Create(0UL, 0, 1, 2, 3, 4, 5, 6);
    private interface IScalar { ulong Next(ulong a, ulong b); }
    private readonly struct Original(int period) : IScalar
    {
        private readonly Baseline.WordSmoother word = new(period);
        public ulong Next(ulong a, ulong b) => unchecked((ulong)BitConverter.DoubleToInt64Bits(word.SmoothSameBinade(a,b,a >> 52)));
    }
    private readonly struct Inline(int period) : IScalar
    {
        private readonly BaselineInline.WordSmoother word = new(period);
        public ulong Next(ulong a, ulong b) => unchecked((ulong)BitConverter.DoubleToInt64Bits(word.SmoothSameBinade(a,b,a >> 52)));
    }

    private static ulong Reference(ulong a, ulong b, int period)
    {
        BigInteger n = new BigInteger((a & Fraction) | (1UL << 52)) * (period - 1)
            + 2 * new BigInteger((b & Fraction) | (1UL << 52));
        BigInteger q = BigInteger.DivRem(n, period + 1, out var rem);
        if (rem * 2 > period + 1 || rem * 2 == period + 1 && !q.IsEven) q++;
        return (a & ~Fraction) | ((ulong)q & Fraction);
    }
    private static void Equal(ulong expected, ulong actual, string context)
    {
        if(expected != actual) throw new InvalidOperationException($"{context}: {expected:x16} != {actual:x16}");
    }
    private static ulong RandomWord(Random r) => ((ulong)(uint)r.NextInt64(0,1L << 32) << 32) | (uint)r.NextInt64(0,1L << 32);
    private static Vector256<ulong> Load256(ulong[] values, int start=0) => Vector256.Create(values[start], values[start+1], values[start+2], values[start+3]);
    private static Vector512<ulong> Load512(ulong[] values) => Vector512.Create(Load256(values), Load256(values,4));

    private static void Correctness()
    {
        var r=new Random(490817); var a=new ulong[8];var b=new ulong[8];
        // All supported periods, every exponent boundary, equality and both
        // significand endpoints. Oracle arithmetic is arbitrary precision.
        for(int p=1;p<=2047;p++)
        for(int sample=0;sample<32;sample++)
        {
            for(int lane=0;lane<8;lane++)
            {
                ulong exponent=(ulong)((sample % 4) switch {0=>1,1=>2046,_=>r.Next(1,2047)});
                ulong left=sample switch {0=>0,1=>Fraction,3=>0,4=>1,_=>RandomWord(r)&Fraction};
                ulong right=sample switch {0=>Fraction,1=>0,2=>left,3 or 4=>left+(ulong)(p+1)/4,_=>RandomWord(r)&Fraction};
                a[lane]=(exponent<<52)|left;b[lane]=(exponent<<52)|right;
            }
            var v256=Avx2.IsSupported ? new Kernel256(p).Smooth(Load256(a),Load256(b)) : default;
            var v512=Avx512F.IsSupported ? new Kernel512(p).Smooth(Load512(a),Load512(b)) : default;
            var original=new Original(p);var inline=new Inline(p);
            for(int lane=0;lane<8;lane++)
            {
                ulong expected=Reference(a[lane],b[lane],p);
                Equal(expected,original.Next(a[lane],b[lane]),"scalar");
                Equal(expected,inline.Next(a[lane],b[lane]),"inline");
                if(Avx2.IsSupported && lane<4) Equal(expected,v256.GetElement(lane),$"vector256/p{p}/s{sample}/lane{lane}");
                if(Avx512F.IsSupported) Equal(expected,v512.GetElement(lane),$"vector512/p{p}/s{sample}/lane{lane}");
                oracleLanes++;
            }
        }
        // Test high-half multiplication independently beyond smoothing's range.
        for(int sample=0;sample<32768;sample++)
        {
            for(int lane=0;lane<8;lane++)
            {
                a[lane]=sample switch {0=>0,1=>ulong.MaxValue,2=>1UL<<63,_=>RandomWord(r)};
                b[lane]=sample switch {0=>ulong.MaxValue,1=>ulong.MaxValue,2=>1UL<<63,_=>RandomWord(r)};
            }
            var v256=Avx2.IsSupported ? Kernel256.HighProduct(Load256(a),Load256(b)) : default;
            var v512=Avx512F.IsSupported ? Kernel512.HighProduct(Load512(a),Load512(b)) : default;
            for(int lane=0;lane<8;lane++)
            {
                ulong expected=(ulong)((new BigInteger(a[lane])*b[lane])>>64);
                if(Avx2.IsSupported && lane<4)Equal(expected,v256.GetElement(lane),"high256");
                if(Avx512F.IsSupported)Equal(expected,v512.GetElement(lane),"high512");
                productLanes++;
            }
        }
        foreach(int p in new[]{1,2,3,14,20,31,2047})
        foreach(int exponent in new[]{1,6,1023,2046})
        {
            var prices=Enumerable.Range(0,257).Select(_=>((ulong)exponent<<52)|(RandomWord(r)&Fraction)).ToArray();
            var expected=new ulong[prices.Length];var state=Enumerable.Repeat(prices[0],6).ToArray();
            for(int i=0;i<prices.Length;i++)
            {
                ulong input=prices[i];
                for(int j=0;j<6;j++){state[j]=Reference(state[j],input,p);input=state[j];}
                expected[i]=input;
            }
            foreach(string method in Methods())
            {
                var actual=new ulong[prices.Length];Run(method,prices,actual,p);
                for(int i=0;i<actual.Length;i++){Equal(expected[i],actual[i],$"chain/{method}");chainValues++;}
            }
        }
        foreach(int p in new[]{0,2048,int.MaxValue})
            if(new Baseline.WordSmoother(p).IsSupported)throw new InvalidOperationException("Unsupported-period fallback guard");
        foreach(double invalid in new[]{0d,-1,double.Epsilon,double.PositiveInfinity,double.NaN})
            if(new Baseline.WordSmoother(14).TrySmooth(invalid,100,out _))throw new InvalidOperationException("Unsupported-input fallback guard");
    }

    private static IEnumerable<string> Methods()
    {
        yield return "scalar-original";yield return "scalar-inline";
        if(Avx2.IsSupported)yield return "vector256";
        if(Avx512F.IsSupported)yield return "vector512";
    }
    private static void Scalar<T>(ulong[] prices,ulong[] output,T word) where T:struct,IScalar
    {
        ulong a=prices[0],b=a,c=a,d=a,e=a,f=a;
        for(int i=0;i<prices.Length;i++)
        {
            a=word.Next(a,prices[i]);b=word.Next(b,a);c=word.Next(c,b);
            d=word.Next(d,c);e=word.Next(e,d);f=word.Next(f,e);output[i]=f;
        }
    }
    private static void VectorChain256(ulong[] prices,ulong[] output,int period)
    {
        var word=new Kernel256(period);var lo=Vector256.Create(prices[0]);var hi=lo;
        for(int tick=0;tick<prices.Length+5;tick++)
        {
            var loInput=Avx2.Permute4x64(lo,0x90).WithElement(0,prices[Math.Min(tick,prices.Length-1)]);
            var hiInput=Avx2.Permute4x64(hi,0x90).WithElement(0,lo.GetElement(3));
            lo=word.Smooth(lo,loInput);hi=word.Smooth(hi,hiInput);
            if(tick>=5)output[tick-5]=hi.GetElement(1);
        }
    }
    private static void VectorChain512(ulong[] prices,ulong[] output,int period)
    {
        var word=new Kernel512(period);var state=Vector512.Create(prices[0]);
        for(int tick=0;tick<prices.Length+5;tick++)
        {
            var input=Avx512F.PermuteVar8x64(state,ShiftIndices).WithElement(0,prices[Math.Min(tick,prices.Length-1)]);
            state=word.Smooth(state,input);
            if(tick>=5)output[tick-5]=state.GetElement(5);
        }
    }
    private static void Run(string method,ulong[] prices,ulong[] output,int period)
    {
        switch(method)
        {
            case "scalar-original":Scalar(prices,output,new Original(period));break;
            case "scalar-inline":Scalar(prices,output,new Inline(period));break;
            case "vector256":VectorChain256(prices,output,period);break;
            case "vector512":VectorChain512(prices,output,period);break;
            default:throw new ArgumentOutOfRangeException(nameof(method));
        }
        sink^=output[^1];
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
    private static (ulong Idle,ulong Total) Cpu()
    {
        if(OperatingSystem.IsWindows())
        {
            if(!GetSystemTimes(out var idle,out var kernel,out var user))throw new InvalidOperationException("GetSystemTimes");
            return(idle,kernel+user);
        }
        var fields=File.ReadLines("/proc/stat").First().Split(' ',StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
        return(fields[3]+fields[4],fields.Aggregate(0UL,(a,b)=>a+b));
    }
    private static List<double> Quiet()
    {
        var values=new List<double>();var deadline=DateTime.UtcNow.AddMinutes(2);var previous=Cpu();
        while(values.Count<12)
        {
            if(DateTime.UtcNow>deadline)throw new TimeoutException("Twelve CPU samples below15% unavailable; no timing claim");
            Thread.Sleep(1000);var next=Cpu();double use=100d*(1d-(double)(next.Idle-previous.Idle)/(next.Total-previous.Total));previous=next;
            if(use>=15)values.Clear();else values.Add(use);
        }
        return values;
    }
    public static void Main(string[] args)
    {
        foreach(string name in new[]{"DOTNET_TieredPGO","DOTNET_TieredCompilation","DOTNET_ReadyToRun","COMPlus_TieredPGO","COMPlus_TieredCompilation","COMPlus_ReadyToRun","COMPlus_JitNoInline"})
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))throw new InvalidOperationException("Runtime override present: "+name);
        var start=DateTime.UtcNow;Correctness();
        Console.WriteLine($"Correctness passed: {oracleLanes} oracle lanes, {productLanes} high-product lanes, {chainValues} chain values; AVX2={Avx2.IsSupported}, AVX512F={Avx512F.IsSupported}");
        File.WriteAllText(args[1].Replace(".json","-correctness.json"),JsonSerializer.Serialize(new{passed=true,oracle_lanes=oracleLanes,high_product_lanes=productLanes,chain_values=chainValues,avx2=Avx2.IsSupported,avx512f=Avx512F.IsSupported,source_commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),assembly_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location)))},new JsonSerializerOptions{WriteIndented=true}));
        var prices=Enumerable.Range(0,4096).Select(i=>unchecked((ulong)BitConverter.DoubleToInt64Bits(100+(i*17%31)/8d))).ToArray();
        var output=new ulong[prices.Length];var rounds=new List<object>();
        foreach(string candidate in Methods().Where(x=>x!="scalar-original"))
        {
            string[] order=["scalar-original",candidate,candidate,"scalar-original"];
            for(int round=0;round<4;round++)
            {
                string method=order[round];
                for(int i=0;i<1000;i++)Run(method,prices,output,14);
                var quiet=Quiet();var samples=new List<double>();
                for(int iteration=0;iteration<10;iteration++)
                {
                    long before=Stopwatch.GetTimestamp();
                    for(int i=0;i<1024;i++)Run(method,prices,output,14);
                    samples.Add(Stopwatch.GetElapsedTime(before).TotalNanoseconds/(1024*prices.Length));
                }
                rounds.Add(new{candidate,round,method,quiet_cpu_percent=quiet,ns_per_input_bar=samples});
                Console.WriteLine($"{candidate}/{round}/{method}: {samples.Average():F3} ns/bar (diagnostic only)");
            }
        }
        var result=new{kind="Diagnostic six-EMA first-price smoothing pipeline only; excludes T3 polynomial, real startup, validation, history and publication. Not source qualification or full-indicator performance.",
            started_utc=start,completed_utc=DateTime.UtcNow,runtime=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,
            architecture=RuntimeInformation.ProcessArchitecture.ToString(),avx2=Avx2.IsSupported,avx512f=Avx512F.IsSupported,
            vector256_accelerated=Vector256.IsHardwareAccelerated,vector512_accelerated=Vector512.IsHardwareAccelerated,
            source_commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),run_id=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
            assembly_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
            source_manifest_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
            correctness=new{oracle_lanes=oracleLanes,high_product_lanes=productLanes,chain_values=chainValues,passed=true},
            policy=new{runtime="default; no environment overrides",order="old/new/new/old",warmups=1000,measured_iterations=10,quiet_samples=12,max_cpu_percent=15},rounds,sink};
        File.WriteAllText(args[1],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
}
