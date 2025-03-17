using OpenTelemetry.Trace;

namespace Clients.Api.Diagnostics;

public class  RateSampler : Sampler
{
    private readonly double _samplingRate;
    private readonly Random _random;
    
    public RateSampler(double samplingRate)
    {
        if (samplingRate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(samplingRate), "Sampling rate must be between 0 and 1.");

        _samplingRate = samplingRate;
        _random = new Random();
    }
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var shouldBeSample = _random.NextDouble() < _samplingRate;
        
        return shouldBeSample ? new SamplingResult(SamplingDecision.RecordAndSample) : new SamplingResult(SamplingDecision.Drop);
    }
}