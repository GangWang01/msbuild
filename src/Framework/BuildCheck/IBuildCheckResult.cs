﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Holder for the reported result of a build cop rule.
/// </summary>
internal interface IBuildCheckResult
{
    /// <summary>
    /// Optional location of the finding (in near future we might need to support multiple locations).
    /// </summary>
    string LocationString { get; }

    /// <summary>
    /// The short code of the diagnostic - e.g. 'BC0201'
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Location of the finding.
    /// </summary>
    IMSBuildElementLocation Location { get; }
    string[] MessageArgs { get; }
    string MessageFormat { get; }

    string FormatMessage();
}
