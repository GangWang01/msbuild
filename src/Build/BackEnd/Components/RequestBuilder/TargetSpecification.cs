﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains information about a target name and reference location.
    /// </summary>
    [DebuggerDisplay("Name={TargetName}")]
    internal class TargetSpecification : ITranslatable
    {
        private string _targetName;
        private ElementLocation _referenceLocation;

        internal TargetBuiltReason _targetBuiltReason;

        /// <summary>
        /// Construct a target specification.
        /// </summary>
        /// <param name="targetName">The name of the target</param>
        /// <param name="referenceLocation">The location from which it was referred.</param>
        /// <param name="targetBuiltReason">Reason the target is being built</param>
        internal TargetSpecification(string targetName, ElementLocation referenceLocation, TargetBuiltReason targetBuiltReason = TargetBuiltReason.None)
        {
            ErrorUtilities.VerifyThrowArgumentLength(targetName);
            ErrorUtilities.VerifyThrowArgumentNull(referenceLocation);

            this._targetName = targetName;
            this._referenceLocation = referenceLocation;
            this._targetBuiltReason = targetBuiltReason;
        }

        private TargetSpecification()
        {
        }

        /// <summary>
        /// Gets or sets the target name
        /// </summary>
        public string TargetName => _targetName;

        public TargetBuiltReason TargetBuiltReason => _targetBuiltReason;

        /// <summary>
        /// Gets or sets the reference location
        /// </summary>
        public ElementLocation ReferenceLocation => _referenceLocation;

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _targetName);
            translator.Translate(ref _referenceLocation, ElementLocation.FactoryForDeserialization);
        }

        internal static TargetSpecification FactoryForDeserialization(ITranslator translator)
        {
            var instance = new TargetSpecification();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
