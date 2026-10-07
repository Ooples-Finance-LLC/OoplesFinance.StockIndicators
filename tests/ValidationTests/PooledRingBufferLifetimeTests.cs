using System.Reflection;
using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PooledRingBufferLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanupIsSafeWhenConstructionNeverAcquiredAnArray(bool finalizer)
    {
        // Model the zero-initialized object left when ArrayPool.Rent throws,
        // without exhausting memory or letting a failing finalizer kill the test host.
        var buffer=(PooledRingBuffer<double>)RuntimeHelpers.GetUninitializedObject(typeof(PooledRingBuffer<double>));
        GC.SuppressFinalize(buffer);
        if(finalizer)
            typeof(PooledRingBuffer<double>).GetMethod("Finalize",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)!.Invoke(buffer,null);
        buffer.Dispose();
        buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>buffer.TryAdd(1,out _));
    }
    [Fact]
    public void RentedBufferRetainsEvictionAndIdempotentDisposal()
    {
        var buffer=new PooledRingBuffer<double>(2);
        Assert.False(buffer.TryAdd(1,out _));Assert.False(buffer.TryAdd(2,out _));
        Assert.True(buffer.TryAdd(3,out var evicted));Assert.Equal(1,evicted);
        Assert.Equal(2,buffer[0]);Assert.Equal(3,buffer[1]);
        buffer.Dispose();buffer.Dispose();Assert.Throws<ObjectDisposedException>(()=>buffer.TryAdd(4,out _));
    }
}
