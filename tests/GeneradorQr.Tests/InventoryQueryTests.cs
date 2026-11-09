using GeneradorQr.Core.Inventory;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Tests;

public class InventoryQueryTests
{
    private static VehicleRecord V(long id, string marca, string modelo, int anio, decimal precio = 100m, bool vendido = false) => new()
    {
        Id = id,
        Marca = marca,
        Modelo = modelo,
        Anio = anio,
        Precio = precio,
        Procedencia = "Nacional",
        MotorLitros = "5.0 L",
        ConfiguracionMotor = "V8",
        Vendido = vendido
    };

    private static readonly VehicleRecord[] All =
    [
        V(1, "Ford", "Lobo", 2020, 400m),
        V(2, "Ford", "Ranger", 2018, 300m),
        V(3, "Nissan", "Versa", 2021, 200m, vendido: true),
        V(4, "ford", "Ranger", 2022, 500m)
    ];

    [Fact]
    public void Search_CadaPalabraEnAlgunCampo()
    {
        var result = InventoryQuery.Search(All, "ford 2020", includeSold: true);
        Assert.Equal([1L], result.Select(v => v.Id));
    }

    [Fact]
    public void Search_NoDistingueMayusculas()
    {
        var result = InventoryQuery.Search(All, "LOBO", includeSold: true);
        Assert.Equal([1L], result.Select(v => v.Id));
    }

    [Fact]
    public void Search_OcultaVendidosSiNoSePiden()
    {
        Assert.DoesNotContain(InventoryQuery.Search(All, null, includeSold: false), v => v.Vendido);
        Assert.Contains(InventoryQuery.Search(All, null, includeSold: true), v => v.Vendido);
    }

    [Fact]
    public void Search_Orden_DisponiblesPrimeroLuegoIdDesc()
    {
        var result = InventoryQuery.Search(All, "", includeSold: true);
        Assert.Equal([4L, 2L, 1L, 3L], result.Select(v => v.Id));
    }

    [Fact]
    public void Stats_SumaSoloDisponibles()
    {
        Assert.Equal(new InventoryStats(3, 1, 1200m), InventoryQuery.Stats(All));
    }

    [Fact]
    public void Distinct_OrdenadoSinVaciosSinDuplicadosIgnorandoMayusculas()
    {
        var conVacio = All.Append(V(5, "", "Aveo", 2015));
        Assert.Equal(["Ford", "Nissan"], InventoryQuery.DistinctMarcas(conVacio));
        Assert.Equal(["Aveo", "Lobo", "Ranger", "Versa"], InventoryQuery.DistinctModelos(conVacio));
    }
}
