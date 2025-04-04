using System.Diagnostics;
using Grpc.Core;
using RiskEvaluator.Diagnostics;
using RiskEvaluator.Services.Rules;

namespace RiskEvaluator.Services;

public class EvaluatorService : Evaluator.EvaluatorBase
{
    private readonly ILogger<EvaluatorService> _logger;
    private readonly IEnumerable<IRule> _rules;

    public EvaluatorService(ILogger<EvaluatorService> logger, IEnumerable<IRule> rules)
    {
        _logger = logger;
        _rules = rules;
    }

    public override Task<RiskEvaluationReply> Evaluate(RiskEvaluationRequest request, ServerCallContext context)
    {
        try
        {
            var score = _rules.Sum(rule => rule.Evaluate(request));

            var level = score switch
            {
                <= 5 => RiskLevel.Low,
                <= 20 => RiskLevel.Medium,
                _ => RiskLevel.High
            };

            Activity.Current?.SetTag(TagNames.EmailEvaluation, request.Email);
            Activity.Current?.AddEvent(new ActivityEvent(
                "RiskResult",
                tags: new ActivityTagsCollection(
                    new KeyValuePair<string, object?>[]
                    {
                        new(TagNames.RiskScore, score),
                        new(TagNames.RiskLevel, level),
                    }
                )));

            return Task.FromResult(new RiskEvaluationReply()
            {
                RiskLevel = level,
            });
        }
        catch (Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error);
            Activity.Current?.AddException(ex);
            return Task.FromResult(new RiskEvaluationReply()
            {
                RiskLevel = RiskLevel.High,
            });
        }
    }
}