using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LSP;

public class CompletionHandler : CompletionHandlerBase
{
    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) => throw new NotImplementedException();
    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken) => throw new NotImplementedException();
    protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions();
    }
}
