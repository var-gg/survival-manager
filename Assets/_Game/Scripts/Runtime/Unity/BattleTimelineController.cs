using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Unity;

public sealed class BattleTimelineController
{
    private readonly List<BattleSimulationStep> _recordedSteps = new();

    private BattleSimulator _simulator = null!;
    private float _stepAccumulator;
    private int _currentIndex;

    public bool IsPaused { get; private set; }
    public float PlaybackSpeed { get; private set; } = 1f;
    public bool IsFinished { get; private set; }
    public int CurrentIndex => _currentIndex;
    public int FurthestIndex => _recordedSteps.Count - 1;
    public int MaxPossibleSteps { get; private set; }
    public float FixedStepSeconds { get; private set; }

    public float NormalizedProgress =>
        MaxPossibleSteps > 0 ? (float)_currentIndex / MaxPossibleSteps : 0f;

    public BattleSimulationStep? CurrentStep =>
        _currentIndex >= 0 && _currentIndex < _recordedSteps.Count ? _recordedSteps[_currentIndex] : null;

    public BattleSimulationStep? PreviousStep
    {
        get
        {
            var prevIndex = Math.Max(0, _currentIndex - 1);
            return prevIndex < _recordedSteps.Count ? _recordedSteps[prevIndex] : null;
        }
    }

    public void Initialize(BattleSimulator simulator, BattleSimulationStep initialStep, int maxSteps)
    {
        _simulator = simulator;
        MaxPossibleSteps = maxSteps;
        FixedStepSeconds = simulator.State.FixedStepSeconds;
        _recordedSteps.Clear();
        _recordedSteps.Add(initialStep);
        _currentIndex = 0;
        _stepAccumulator = 0f;
        IsPaused = false;
        IsFinished = false;
        PlaybackSpeed = 1f;
    }

    public void Reset(BattleSimulator simulator, BattleSimulationStep initialStep, int maxSteps)
    {
        Initialize(simulator, initialStep, maxSteps);
    }

    /// <summary>
    /// Called each frame. Advances playback if not paused. Returns true when a new step was consumed
    /// (caller should refresh HUD). Always sets out parameters for blend interpolation.
    /// </summary>
    public bool TryAdvance(float deltaTime,
        out BattleSimulationStep previousStep,
        out BattleSimulationStep currentStep,
        out float alpha)
    {
        previousStep = PreviousStep!;
        currentStep = CurrentStep!;
        alpha = IsFinished ? 1f : 0f;

        if (previousStep == null || currentStep == null)
        {
            return false;
        }

        if (IsPaused || IsFinished || _simulator.IsFinished && _currentIndex >= FurthestIndex)
        {
            alpha = 1f;
            return false;
        }

        var stepped = false;
        _stepAccumulator += deltaTime * PlaybackSpeed;

        while (_stepAccumulator >= FixedStepSeconds)
        {
            _stepAccumulator -= FixedStepSeconds;

            if (_currentIndex < FurthestIndex)
            {
                _currentIndex++;
            }
            else if (!_simulator.IsFinished)
            {
                var newStep = _simulator.Step();
                _recordedSteps.Add(newStep);
                _currentIndex = _recordedSteps.Count - 1;

                if (newStep.IsFinished)
                {
                    MarkFinished();
                }
            }
            else
            {
                _stepAccumulator = 0f;
                break;
            }

            stepped = true;
            previousStep = PreviousStep!;
            currentStep = CurrentStep!;
        }

        var fixedStep = Math.Max(0.0001f, FixedStepSeconds);
        alpha = IsFinished ? 1f : Math.Min(1f, Math.Max(0f, _stepAccumulator / fixedStep));

        return stepped;
    }

    public void SeekToStep(int targetIndex)
    {
        targetIndex = Math.Max(0, Math.Min(targetIndex, MaxPossibleSteps));

        // Backward seek: instant (data already recorded)
        if (targetIndex <= FurthestIndex)
        {
            _currentIndex = targetIndex;
            _stepAccumulator = 0f;

            // If we seeked back from finished, we might be "unfinished" from playback perspective
            // but the simulation itself may already be done. IsFinished reflects whether we've
            // reached the end of the recorded battle in our playback position.
            if (targetIndex < FurthestIndex || !_simulator.IsFinished)
            {
                IsFinished = false;
            }

            return;
        }

        // Forward seek: step simulator until target
        while (FurthestIndex < targetIndex && !_simulator.IsFinished)
        {
            var newStep = _simulator.Step();
            _recordedSteps.Add(newStep);

            if (newStep.IsFinished)
            {
                MarkFinished();
                break;
            }
        }

        _currentIndex = Math.Min(targetIndex, FurthestIndex);
        _stepAccumulator = 0f;
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    public void SetSpeed(float speed)
    {
        PlaybackSpeed = Math.Max(0.25f, Math.Min(speed, 8f));
    }

    public void MarkFinished()
    {
        IsFinished = true;
        _stepAccumulator = 0f;
    }

    public void StepOnce()
    {
        if (IsFinished && _currentIndex >= FurthestIndex) return;

        if (_currentIndex < FurthestIndex)
        {
            _currentIndex++;
        }
        else if (!_simulator.IsFinished)
        {
            var newStep = _simulator.Step();
            _recordedSteps.Add(newStep);
            _currentIndex = _recordedSteps.Count - 1;

            if (newStep.IsFinished)
            {
                MarkFinished();
            }
        }
    }
}
