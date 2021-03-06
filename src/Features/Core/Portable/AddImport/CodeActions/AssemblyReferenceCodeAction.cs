﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.AddImport
{
    internal abstract partial class AbstractAddImportCodeFixProvider<TSimpleNameSyntax>
    {
            private class AssemblyReferenceCodeAction : AddImportCodeAction
            {
                private readonly string _assemblyName;
                private readonly string _fullyQualifiedTypeName;

                private readonly Lazy<string> _lazyResolvedPath;

                public AssemblyReferenceCodeAction(
                    Document originalDocument,
                    ImmutableArray<TextChange> textChanges,
                    string title,
                    string assemblyName,
                    string fullyQualifiedTypeName)
                    : base(originalDocument, textChanges, title, WellKnownTagArrays.AddReference, CodeActionPriority.Low)
                {
                    _assemblyName = assemblyName;
                    _fullyQualifiedTypeName = fullyQualifiedTypeName;

                    _lazyResolvedPath = new Lazy<string>(ResolvePath);
                }

                private string ResolvePath()
                {
                    var assemblyResolverService = OriginalDocument.Project.Solution.Workspace.Services.GetService<IFrameworkAssemblyPathResolver>();

                    var assemblyPath = assemblyResolverService?.ResolveAssemblyPath(
                        OriginalDocument.Project.Id, _assemblyName, _fullyQualifiedTypeName);

                    return assemblyPath;
                }

                internal override bool PerformFinalApplicabilityCheck
                    => true;

                internal override bool IsApplicable(Workspace workspace)
                    => !string.IsNullOrWhiteSpace(_lazyResolvedPath.Value);

                protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
                {
                    var service = OriginalDocument.Project.Solution.Workspace.Services.GetService<IMetadataService>();
                    var resolvedPath = _lazyResolvedPath.Value;
                    var reference = service.GetReference(resolvedPath, MetadataReferenceProperties.Assembly);
    
                    var newDocument = await GetUpdatedDocumentAsync(cancellationToken).ConfigureAwait(false);

                    // Now add the actual assembly reference.
                    var newProject = newDocument.Project;
                    newProject = newProject.WithMetadataReferences(
                        newProject.MetadataReferences.Concat(reference));

                    var operation = new ApplyChangesOperation(newProject.Solution);
                    return SpecializedCollections.SingletonEnumerable<CodeActionOperation>(operation);
                }
            }
    }
}