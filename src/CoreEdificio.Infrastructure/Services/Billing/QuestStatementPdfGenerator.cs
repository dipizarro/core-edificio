using CoreEdificio.Application.Contracts.Billing.Statement;
using CoreEdificio.Application.Interfaces.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoreEdificio.Infrastructure.Services.Billing;

public class QuestStatementPdfGenerator : IStatementPdfGenerator
{
    static QuestStatementPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateUnitStatementPdfAsync(UnitStatementDto statement, CancellationToken ct = default)
    {
        var emissionDate = DateTime.Now;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Text("BOLETA DE GASTOS COMUNES").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text($"Comunidad: {statement.CommunityId}");
                    col.Item().Text($"Unidad: {statement.UnitNumber}");
                    col.Item().Text($"Periodo: {statement.Period}");
                    col.Item().Text($"Total a Pagar: {statement.TotalDue:C0}");
                    
                    col.Item().PaddingVertical(10);
                    col.Item().Text("Detalle disponible en próxima versión (Layout en ajuste).");
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }
}
