﻿using Maestro.Wpf;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;


public class OpenInformationWindowHandler(GuiInvoker guiInvoker)
    : IRequestHandler<OpenInformationWindowRequest, OpenInformationWindowResponse>
{
    public Task<OpenInformationWindowResponse> Handle(OpenInformationWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var child = new InformationView(request.Flight);
            var form = new VatSysForm(
                title: request.Flight.Callsign,
                child,
                shrinkToContent: true);
            
            form.Show(mainForm);
        });
        
        return Task.FromResult(new OpenInformationWindowResponse());
    }
}