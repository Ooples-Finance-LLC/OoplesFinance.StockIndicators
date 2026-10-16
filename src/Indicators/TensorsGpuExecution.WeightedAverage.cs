#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompileWeightedAverage()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void weighted_average(__global const double* input, __global const double* unused,
                __global double* output, int count, int period, int normalize_available, int first_weight, int weight_step)
            {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                if (period == 1) {
                    for (size_t i = start; i < end; i++)
                        output[i] = normalize_available == 8 || normalize_available == 5 && first_weight == 0 ? 0.0
                            : input[i] == 0.0 ? 0.0 : input[i];
                    return;
                }
                if (normalize_available >= 6) {
                    // Exact centered regression from S=sum(y), L=sum(lag*y),
                    // where newest lag is zero. All products and totals are
                    // certified on the same binary grid under 2*P*P terms.
                    double sum = 0.0, lagged = 0.0;
                    size_t first = start >= (size_t)(period - 1) ? start - (size_t)(period - 1) : 0;
                    for (size_t j = first; j < start; j++) {
                        sum += input[j];
                        lagged += input[j] * (double)(start - j);
                    }
                    for (size_t i = start; i < end; i++) {
                        sum += input[i];
                        double n = (double)min(i + 1, (size_t)period);
                        if (n == 1.0) output[i] = normalize_available == 8 || sum == 0.0 ? 0.0 : sum;
                        else {
                            double numerator, denominator;
                            if (normalize_available == 8) {
                                numerator = (n - 1.0) * sum - 2.0 * lagged;
                                // P <= 32767 makes n*(n*n-1) an exactly
                                // representable integer; it is divisible by six.
                                denominator = n * (n * n - 1.0) / 6.0;
                            } else if (normalize_available == 9) {
                                numerator = (2.0 - n) * sum + 3.0 * lagged;
                                denominator = n * (n + 1.0) / 2.0;
                            } else {
                                numerator = (2.0 * n - (normalize_available == 7 ? 2.0 : 1.0)) * sum - 3.0 * lagged;
                                denominator = n * (n + (normalize_available == 7 ? -1.0 : 1.0)) / 2.0;
                            }
                            output[i] = numerator == 0.0 ? 0.0 : numerator / denominator;
                        }
                        lagged += sum;
                        if (i >= (size_t)(period - 1)) {
                            double expired = input[i - (size_t)(period - 1)];
                            lagged -= expired * period;
                            sum -= expired;
                        }
                    }
                    return;
                }
                if (normalize_available >= 3) {
                    // Newest-first integer affine weights. Maintain
                    // exact sum and weighted sum, rounding only the final quotient.
                    double sum = 0.0, weighted = 0.0;
                    size_t first = start >= (size_t)(period - 1) ? start - (size_t)(period - 1) : 0;
                    for (size_t j = first; j < start; j++) {
                        double value = input[j];
                        sum += value;
                        weighted += value * ((double)first_weight + (double)weight_step * (start - j));
                    }
                    for (size_t i = start; i < end; i++) {
                        weighted += input[i] * first_weight;
                        sum += input[i];
                        int available = (int)min(i + 1, (size_t)period);
                        if (normalize_available == 4 && available < period)
                            output[i] = sum == 0.0 ? 0.0 : sum / available;
                        else {
                            double mass = (double)available * first_weight
                                + (double)weight_step * available * (available - 1) / 2.0;
                            if (mass == 0.0) mass = available;
                            output[i] = weighted == 0.0 ? 0.0 : weighted / mass;
                        }
                        weighted += (double)weight_step * sum;
                        if (i >= (size_t)(period - 1)) {
                            double expired = input[i - (size_t)(period - 1)];
                            weighted -= expired * ((double)first_weight + (double)weight_step * period);
                            sum -= expired;
                        }
                    }
                    return;
                }
                if (normalize_available == 2) {
                    size_t i = start;
                    for (; i < end && i < (size_t)(period - 1); i++) output[i] = 0.0;
                    if (i == end) return;
                    int a = (period + 1) / 2, b = period - a + 1;
                    double mass = (double)a * b;
                    double weighted = 0.0, left = 0.0, right = 0.0;
                    size_t first = i - (size_t)(period - 1);
                    for (int j = 0; j < period; j++) {
                        double value = input[first + (size_t)j];
                        weighted += value * min(j + 1, period - j);
                        if (j < a) left += value;
                        if (j >= period - a) right += value;
                    }
                    for (; i < end; i++) {
                        output[i] = weighted == 0.0 ? 0.0 : weighted / mass;
                        if (i + 1 == end) break;
                        // Two unrounded box sums update the triangular total.
                        // Remove before adding to stay within its certified mass.
                        weighted -= left;
                        right -= input[i - (size_t)(a - 1)];
                        right += input[i + 1];
                        weighted += right;
                        left -= input[i - (size_t)(period - 1)];
                        left += input[i - (size_t)(b - 2)];
                    }
                    return;
                }
                double sum = 0.0, weighted = 0.0;
                double denominator = (double)period * (period + 1) / 2.0;
                size_t first = start >= (size_t)(period - 1) ? start - (size_t)(period - 1) : 0;
                for (size_t j = first; j < start; j++) {
                    sum += input[j];
                    weighted += input[j] * (double)((size_t)period - (start - j));
                }
                for (size_t i = start; i < end; i++) {
                    weighted += input[i] * period;
                    sum += input[i];
                    double mass = denominator;
                    if (normalize_available && i < (size_t)period) {
                        double available = (double)(i + 1);
                        mass = available * (2.0 * period - available + 1.0) / 2.0;
                    }
                    output[i] = weighted == 0.0 ? 0.0 : weighted / mass;
                    // Subtract while sum still contains P terms. This lowers each
                    // coefficient by one, leaving total weight at most T-P.
                    weighted -= sum;
                    if (i >= (size_t)(period - 1)) sum -= input[i - (size_t)(period - 1)];
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "weighted_average"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
