using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Agents.Builders;

/// <summary>
/// Fluent builder for workflow state test data.
/// </summary>
public sealed class WorkflowStateBuilder
{
    private Guid _eventId = Guid.CreateVersion7();
    private WorkflowPhase _currentPhase = WorkflowPhase.Planning;
    private WorkflowStatus _status = WorkflowStatus.InProgress;
    private DateTime _lastUpdated = DateTime.UtcNow;

    public static WorkflowStateBuilder CreateWorkflowState() => new();

    public WorkflowStateBuilder WithEventId(Guid eventId)
    {
        _eventId = eventId;
        return this;
    }

    public WorkflowStateBuilder WithCurrentPhase(WorkflowPhase phase)
    {
        _currentPhase = phase;
        return this;
    }

    public WorkflowStateBuilder WithStatus(WorkflowStatus status)
    {
        _status = status;
        return this;
    }

    public WorkflowStateBuilder WithLastUpdated(DateTime lastUpdated)
    {
        _lastUpdated = lastUpdated;
        return this;
    }

    public WorkflowState Build()
    {
        return new WorkflowState
        {
            EventId = _eventId,
            CurrentPhase = _currentPhase,
            Status = _status,
            LastUpdated = _lastUpdated
        };
    }
}
