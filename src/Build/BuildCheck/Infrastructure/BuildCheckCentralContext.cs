// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// A manager of the runs of the checks - deciding based on configuration of what to run and what to postfilter.
/// </summary>
internal sealed class BuildCheckCentralContext
{
    private readonly IConfigurationProvider _configurationProvider;

    public BuildCheckCentralContext(
        IConfigurationProvider configurationProvider,
        Action<List<CheckWrapper>?, ICheckContext> removeCheck)
    {
        _configurationProvider = configurationProvider;
        _removeChecks = removeCheck;
    }

    private record CallbackRegistry(
        List<(CheckWrapper, Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>>)> EvaluatedPropertiesActions,
#pragma warning disable CS0618 // Type or member is obsolete
        List<(CheckWrapper, Action<BuildCheckDataContext<ParsedItemsCheckData>>)> ParsedItemsActions,
#pragma warning restore CS0618 // Type or member is obsolete
        List<(CheckWrapper, Action<BuildCheckDataContext<EvaluatedItemsCheckData>>)> EvaluatedItemsActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<TaskInvocationCheckData>>)> TaskInvocationActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<PropertyReadData>>)> PropertyReadActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<PropertyWriteData>>)> PropertyWriteActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>>)> ProjectRequestProcessingDoneActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<BuildFinishedCheckData>>)> BuildFinishedActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<EnvironmentVariableCheckData>>)> EnvironmentVariableCheckDataActions,
        List<(CheckWrapper, Action<BuildCheckDataContext<ProjectImportedCheckData>>)> ProjectImportedCheckDataActions)
    {
        public CallbackRegistry()
            : this([], [], [], [], [], [], [], [], [], [])
        {
        }

        internal void DeregisterCheck(CheckWrapper check)
        {
            EvaluatedPropertiesActions.RemoveAll(a => a.Item1 == check);
            ParsedItemsActions.RemoveAll(a => a.Item1 == check);
            EvaluatedItemsActions.RemoveAll(a => a.Item1 == check);
            PropertyReadActions.RemoveAll(a => a.Item1 == check);
            PropertyWriteActions.RemoveAll(a => a.Item1 == check);
            ProjectRequestProcessingDoneActions.RemoveAll(a => a.Item1 == check);
            BuildFinishedActions.RemoveAll(a => a.Item1 == check);
        }
    }

    // In a future we can have callbacks per project as well
    private readonly CallbackRegistry _globalCallbacks = new();
    private readonly Action<List<CheckWrapper>?, ICheckContext> _removeChecks;


    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when checks might not be known yet
    internal bool HasEvaluatedPropertiesActions => _globalCallbacks.EvaluatedPropertiesActions.Count > 0;

    internal bool HasParsedItemsActions => _globalCallbacks.ParsedItemsActions.Count > 0;

    internal bool HasTaskInvocationActions => _globalCallbacks.TaskInvocationActions.Count > 0;

    internal bool HasPropertyReadActions => _globalCallbacks.PropertyReadActions.Count > 0;

    internal bool HasPropertyWriteActions => _globalCallbacks.PropertyWriteActions.Count > 0;

    internal bool HasBuildFinishedActions => _globalCallbacks.BuildFinishedActions.Count > 0;

    internal void RegisterEnvironmentVariableReadAction(CheckWrapper check, Action<BuildCheckDataContext<EnvironmentVariableCheckData>> environmentVariableAction)
       => RegisterAction(check, environmentVariableAction, _globalCallbacks.EnvironmentVariableCheckDataActions);

    internal void RegisterEvaluatedPropertiesAction(CheckWrapper check, Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction)
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        => RegisterAction(check, evaluatedPropertiesAction, _globalCallbacks.EvaluatedPropertiesActions);

#pragma warning disable CS0618 // Type or member is obsolete
    internal void RegisterParsedItemsAction(CheckWrapper check, Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction)
#pragma warning restore CS0618 // Type or member is obsolete
        => RegisterAction(check, parsedItemsAction, _globalCallbacks.ParsedItemsActions);

    internal void RegisterEvaluatedItemsAction(CheckWrapper check, Action<BuildCheckDataContext<EvaluatedItemsCheckData>> parsedItemsAction)
        => RegisterAction(check, parsedItemsAction, _globalCallbacks.EvaluatedItemsActions);

    internal void RegisterTaskInvocationAction(CheckWrapper check, Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction)
        => RegisterAction(check, taskInvocationAction, _globalCallbacks.TaskInvocationActions);

    internal void RegisterPropertyReadAction(CheckWrapper check, Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction)
        => RegisterAction(check, propertyReadAction, _globalCallbacks.PropertyReadActions);

    internal void RegisterPropertyWriteAction(CheckWrapper check, Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction)
        => RegisterAction(check, propertyWriteAction, _globalCallbacks.PropertyWriteActions);

    internal void RegisterProjectRequestProcessingDoneAction(CheckWrapper check, Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> projectDoneAction)
        => RegisterAction(check, projectDoneAction, _globalCallbacks.ProjectRequestProcessingDoneActions);

    internal void RegisterBuildFinishedAction(CheckWrapper check, Action<BuildCheckDataContext<BuildFinishedCheckData>> buildFinishedAction)
        => RegisterAction(check, buildFinishedAction, _globalCallbacks.BuildFinishedActions);

    internal void RegisterProjectImportedAction(CheckWrapper check, Action<BuildCheckDataContext<ProjectImportedCheckData>> projectImportedAction)
        => RegisterAction(check, projectImportedAction, _globalCallbacks.ProjectImportedCheckDataActions);

    private void RegisterAction<T>(
        CheckWrapper wrappedCheck,
        Action<BuildCheckDataContext<T>> handler,
        List<(CheckWrapper, Action<BuildCheckDataContext<T>>)> handlersRegistry)
        where T : CheckData
    {
        void WrappedHandler(BuildCheckDataContext<T> context)
        {
            using var _ = wrappedCheck.StartSpan();
            handler(context);
        }

        lock (handlersRegistry)
        {
            handlersRegistry.Add((wrappedCheck, WrappedHandler));
        }
    }

    internal void DeregisterCheck(CheckWrapper check) => _globalCallbacks.DeregisterCheck(check);

    internal void RunEnvironmentVariableActions(
        EnvironmentVariableCheckData environmentVariableCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EnvironmentVariableCheckDataActions, environmentVariableCheckData, checkContext, resultHandler);

    internal void RunEvaluatedPropertiesActions(
        EvaluatedPropertiesCheckData evaluatedPropertiesCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EvaluatedPropertiesActions, evaluatedPropertiesCheckData, checkContext, resultHandler);

    internal void RunParsedItemsActions(
#pragma warning disable CS0618 // Type or member is obsolete
        ParsedItemsCheckData parsedItemsCheckData,
#pragma warning restore CS0618 // Type or member is obsolete
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ParsedItemsActions, parsedItemsCheckData,
            checkContext, resultHandler);

    internal void RunEvaluatedItemsActions(
        EvaluatedItemsCheckData evaluatedItemsCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EvaluatedItemsActions, evaluatedItemsCheckData,
            checkContext, resultHandler);

    internal void RunTaskInvocationActions(
        TaskInvocationCheckData taskInvocationCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.TaskInvocationActions, taskInvocationCheckData,
            checkContext, resultHandler);

    internal void RunPropertyReadActions(
        PropertyReadData propertyReadDataData,
        CheckLoggingContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.PropertyReadActions, propertyReadDataData,
            checkContext, resultHandler);

    internal void RunPropertyWriteActions(
        PropertyWriteData propertyWriteData,
        CheckLoggingContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.PropertyWriteActions, propertyWriteData,
            checkContext, resultHandler);

    internal void RunProjectProcessingDoneActions(
        ProjectRequestProcessingDoneData projectProcessingDoneData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ProjectRequestProcessingDoneActions, projectProcessingDoneData,
            checkContext, resultHandler);

    internal void RunBuildFinishedActions(
        BuildFinishedCheckData buildFinishedCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult> resultHandler)
        => RunRegisteredActions(_globalCallbacks.BuildFinishedActions, buildFinishedCheckData, checkContext, resultHandler);

    internal void RunProjectImportedActions(
        ProjectImportedCheckData projectImportedCheckData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult> resultHandler)
        => RunRegisteredActions(_globalCallbacks.ProjectImportedCheckDataActions, projectImportedCheckData, checkContext, resultHandler);

    private void RunRegisteredActions<T>(
        List<(CheckWrapper, Action<BuildCheckDataContext<T>>)> registeredCallbacks,
        T checkData,
        ICheckContext checkContext,
        Action<CheckWrapper, ICheckContext, CheckConfigurationEffective[], BuildCheckResult> resultHandler)
    where T : CheckData
    {
        string projectFullPath = checkData.ProjectFilePath;
        List<CheckWrapper>? checksToRemove = null;

        foreach (var checkCallback in registeredCallbacks)
        {
            // Tracing - https://github.com/dotnet/msbuild/issues/9629 - we might want to account this entire block
            //  to the relevant check (with BuildCheckConfigurationEffective only the currently accounted part as being the 'core-execution' subspan)

            CheckConfigurationEffective? commonConfig = checkCallback.Item1.CommonConfig;
            CheckConfigurationEffective[] configPerRule;

            if (commonConfig != null)
            {
                if (!commonConfig.IsEnabled)
                {
                    return;
                }

                configPerRule = [commonConfig];
            }
            else
            {
                configPerRule = _configurationProvider.GetMergedConfigurations(projectFullPath, checkCallback.Item1.Check);
                if (configPerRule.All(c => !c.IsEnabled))
                {
                    return;
                }
            }

            // Here we might want to check the configPerRule[0].EvaluationsCheckScope - if the input data supports that
            // The decision and implementation depends on the outcome of the investigation tracked in:
            // https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57851137
            BuildCheckDataContext<T> context = new BuildCheckDataContext<T>(
                checkCallback.Item1,
                checkContext,
                configPerRule,
                resultHandler,
                checkData);

            try
            {
                checkCallback.Item2(context);
            }
            catch (Exception e)
            {
                checkContext.DispatchAsWarningFromText(
                    null,
                    null,
                    null,
                    new BuildEventFileInfo(projectFullPath),
                    $"The check '{checkCallback.Item1.Check.FriendlyName}' threw an exception while executing a registered action with message: {e.Message}");

                checksToRemove = checksToRemove ?? new List<CheckWrapper>();
                checksToRemove.Add(checkCallback.Item1);
            }
        }

        _removeChecks(checksToRemove, checkContext);
    }
}
