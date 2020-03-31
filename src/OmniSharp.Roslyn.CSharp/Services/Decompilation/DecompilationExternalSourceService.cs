﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Services;
using OmniSharp.Utilities;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    // due to dependency on Microsoft.CodeAnalysis.Editor.CSharp
    // this class supports only net472
    public class DecompilationExternalSourceService : BaseExternalSourceService, IExternalSourceService
    {
        private const string DecompiledKey = "$Decompiled$";
        private readonly ILoggerFactory _loggerFactory;
        private readonly OmniSharpCSharpDecompiledSourceService _service;

        public DecompilationExternalSourceService(IAssemblyLoader loader, ILoggerFactory loggerFactory, HostLanguageServices hostLanguageServices) : base(loader)
        {
            _loggerFactory = loggerFactory;
            _service = new OmniSharpCSharpDecompiledSourceService(hostLanguageServices, _loader, _loggerFactory);
        }

        public async Task<(Document document, string documentPath)> GetAndAddExternalSymbolDocument(Project project, ISymbol symbol, CancellationToken cancellationToken)
        {
            var fileName = symbol.GetFilePathForExternalSymbol(project);

            Project decompilationProject;

            // since submission projects cannot have new documents added to it
            // we will use a separate project to hold decompiled documents
            if (project.IsSubmission)
            {
                decompilationProject = project.Solution.Projects.FirstOrDefault(x => x.Name == DecompiledKey);
                if (decompilationProject == null)
                {
                    decompilationProject = project.Solution.AddProject(DecompiledKey, $"{DecompiledKey}.dll", LanguageNames.CSharp)
                        .WithCompilationOptions(project.CompilationOptions)
                        .WithMetadataReferences(project.MetadataReferences);
                }
            }
            else
            {
                // for regular projects we will use current project to store decompiled docs
                decompilationProject = project;
            }

            if (!_cache.TryGetValue(fileName, out var document))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
                var temporaryDocument = decompilationProject.AddDocument(fileName, string.Empty);

                var compilation = await decompilationProject.GetCompilationAsync();
                document = await _service.AddSourceToAsync(temporaryDocument, compilation, topLevelSymbol, cancellationToken);

                _cache.TryAdd(fileName, document);
            }

            return (document, fileName);
        }
    }
}
