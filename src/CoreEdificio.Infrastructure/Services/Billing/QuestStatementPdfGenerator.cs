using CoreEdificio.Application.Contracts.Billing.Statement;
using CoreEdificio.Application.Interfaces.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace CoreEdificio.Infrastructure.Services.Billing;

public class QuestStatementPdfGenerator : IStatementPdfGenerator
{
    static QuestStatementPdfGenerator()
    {
        // QuestPDF is free for community use (open source) or requires a license for commercial use.
        // As an AI, I suggest setting this to Community for development/demo purposes.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateUnitStatementPdfAsync(UnitStatementDto statement, CancellationToken ct = default)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ESTADO DE CUENTA").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                        col.Item().Text($"{statement.Period}").FontSize(14);
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text("CORE EDIFICIO").FontSize(16).SemiBold();
                        col.Item().Text($"Unidad: {statement.UnitNumber}");
                        col.Item().Text($"Vencimiento: {statement.DueDate:yyyy-MM-dd}");
                    });
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Resumen de Saldos
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Border(1).Padding(5).Column(c =>
                        {
                            c.Item().Text("Saldo Anterior").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text($"{statement.PreviousBalance:N0}").FontSize(12).SemiBold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).Padding(5).Column(c =>
                        {
                            c.Item().Text("Cargos del Mes").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text($"{statement.CurrentChargesTotal:N0}").FontSize(12).SemiBold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).Padding(5).Column(c =>
                        {
                            c.Item().Text("Pagos Realizados").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text($"{statement.PaymentsTotal:N0}").FontSize(12).SemiBold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Blue.Lighten5).Border(1).Padding(5).Column(c =>
                        {
                            c.Item().Text("TOTAL A PAGAR").FontSize(9).SemiBold().FontColor(Colors.Blue.Medium);
                            c.Item().Text($"{statement.TotalDue:N0}").FontSize(12).SemiBold().FontColor(Colors.Blue.Medium);
                        });
                    });

                    // Desglose de Coeficientes
                    col.Item().PaddingVertical(5).Text("DESGLOSE DE UNIDAD").FontSize(12).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Tipo");
                            header.Cell().Element(CellStyle).Text("Código");
                            header.Cell().Element(CellStyle).AlignRight().Text("Coeficiente (%)");

                            static IContainer CellStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                            }
                        });

                        foreach (var component in statement.Components)
                        {
                            table.Cell().Element(CellStyle).Text(component.Type);
                            table.Cell().Element(CellStyle).Text(component.Code);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{component.CoefficientPct:F4}%");

                            IContainer CellStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }

                        table.Cell().ColumnSpan(2).PaddingVertical(5).AlignRight().Text("Total Coeficiente:").SemiBold();
                        table.Cell().PaddingVertical(5).AlignRight().Text($"{statement.UnitTotalCoefficientPct:F4}%").SemiBold();
                    });

                    // Líneas de Detalle
                    col.Item().PaddingTop(15).PaddingBottom(5).Text("DETALLE DE MOVIMIENTOS").FontSize(12).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Fecha");
                            header.Cell().Element(CellStyle).Text("Tipo");
                            header.Cell().Element(CellStyle).Text("Descripción");
                            header.Cell().Element(CellStyle).AlignRight().Text("Monto");

                            IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                        });

                        foreach (var line in statement.Lines)
                        {
                            table.Cell().Element(CellStyle).Text($"{line.Date:yyyy-MM-dd}");
                            table.Cell().Element(CellStyle).Text(line.Type);
                            table.Cell().Element(CellStyle).Text(line.Description);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{line.Amount:N0}");

                            IContainer CellStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    });
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return Task.FromResult(stream.ToArray());
    }
}
