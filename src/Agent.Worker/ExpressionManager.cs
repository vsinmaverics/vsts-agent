using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using DT = Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Expressions;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    [ServiceLocator(Default = typeof(ExpressionManager))]
    public interface IExpressionManager : IAgentService
    {
        DT.INode Parse(IExecutionContext context, string condition);
        bool Evaluate(IExecutionContext context, DT.INode tree, bool hostTracingOnly = false);
    }

    public sealed class ExpressionManager : AgentService, IExpressionManager
    {
        public DT.INode Parse(IExecutionContext executionContext, string condition)
        {
            ArgUtil.NotNull(executionContext, nameof(executionContext));
            var expressionTrace = new TraceWriter(Trace, executionContext);
            var parser = new DT.Parser();
            var extensions = new DT.IFunctionInfo[]
            {
                new DT.FunctionInfo<AlwaysNode>(name: Constants.Expressions.Always, minParameters: 0, maxParameters: 0),
                new DT.FunctionInfo<SucceededNode>(name: Constants.Expressions.Succeeded, minParameters: 0, maxParameters: 0),
                new DT.FunctionInfo<SucceededOrFailedNode>(name: Constants.Expressions.SucceededOrFailed, minParameters: 0, maxParameters: 0),
                new DT.FunctionInfo<VariablesNode>(name: Constants.Expressions.Variables, minParameters: 1, maxParameters: 1),
            };
            return parser.CreateTree(condition, expressionTrace, extensions) ?? new SucceededNode();
        }

        public bool Evaluate(IExecutionContext executionContext, DT.INode tree, bool hostTracingOnly = false)
        {
            ArgUtil.NotNull(executionContext, nameof(executionContext));
            ArgUtil.NotNull(tree, nameof(tree));
            var expressionTrace = new TraceWriter(Trace, hostTracingOnly ? null : executionContext);
            return tree.EvaluateBoolean(trace: expressionTrace, state: executionContext);
        }

        private sealed class TraceWriter : DT.ITraceWriter
        {
            private readonly IExecutionContext _executionContext;
            private readonly Tracing _trace;

            public TraceWriter(Tracing trace, IExecutionContext executionContext)
            {
                ArgUtil.NotNull(trace, nameof(trace));
                _trace = trace;
                _executionContext = executionContext;
            }

            public void Info(string message)
            {
                _trace.Info(message);
                _executionContext?.Output(message);
            }

            public void Verbose(string message)
            {
                _trace.Verbose(message);
                _executionContext?.Debug(message);
            }
        }

        private sealed class AlwaysNode : DT.FunctionNode
        {
            protected sealed override object EvaluateCore(DT.EvaluationContext evaluationContext)
            {
                return true;
            }
        }

        private sealed class SucceededNode : DT.FunctionNode
        {
            protected sealed override object EvaluateCore(DT.EvaluationContext evaluationContext)
            {
                var executionContext = evaluationContext.State as IExecutionContext;
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                if (executionContext.CancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                TaskResult jobStatus = executionContext.Variables.Agent_JobStatus ?? TaskResult.Succeeded;
                return jobStatus == TaskResult.Succeeded ||
                    jobStatus == TaskResult.SucceededWithIssues;
            }
        }

        private sealed class SucceededOrFailedNode : DT.FunctionNode
        {
            protected sealed override object EvaluateCore(DT.EvaluationContext evaluationContext)
            {
                var executionContext = evaluationContext.State as IExecutionContext;
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                if (executionContext.CancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                TaskResult jobStatus = executionContext.Variables.Agent_JobStatus ?? TaskResult.Succeeded;
                return jobStatus == TaskResult.Succeeded ||
                    jobStatus == TaskResult.SucceededWithIssues ||
                    jobStatus == TaskResult.Failed;
            }
        }

        private sealed class VariablesNode : DT.FunctionNode
        {
            protected sealed override object EvaluateCore(DT.EvaluationContext evaluationContext)
            {
                var executionContext = evaluationContext.State as IExecutionContext;
                ArgUtil.NotNull(executionContext, nameof(executionContext));
                string variableName = Parameters[0].EvaluateString(evaluationContext);
                return executionContext.Variables.Get(variableName);
            }
        }
    }
}