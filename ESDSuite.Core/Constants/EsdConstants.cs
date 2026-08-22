using ESDSuite.Core.Models;

namespace ESDSuite.Core.Constants;

public static class EsdConstants
{
    public const string SystemVersion = "2.7.1";

    public static readonly Dictionary<string, EsdElementInfo> InfoElementosEsd = new()
    {
        { "Pulsera antiestática", new EsdElementInfo { Limite = "RS < 3.5x10^7 ohms", RefNum = 3.5e7, TipoMaterial = "Banda elástica / Metal", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Calzado", new EsdElementInfo { Limite = "RS < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Suela disipativa / Talón", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Piso ESD", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms / Walking Test < 100V", RefNum = 1.0e9, TipoMaterial = "Epóxico / Vinílico ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53 / ANSI/ESD 97.2", Frecuencia = "Semestralmente" } },
        { "Superficie de trabajo", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Tapete disipativo / Mesa", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Anualmente" } },
        { "Monitor Continuo", new EsdElementInfo { Limite = "RTG < 2 ohms", RefNum = 2.0, TipoMaterial = "Equipo Electrónico", Magnitud = "Resistencia", Metodo = "Anexo A.1", Frecuencia = "Trimestralmente" } },
        { "Ionizador", new EsdElementInfo { Limite = "Descarga: <10s, Bal: +-35V", RefNum = 10.0, TipoMaterial = "Ventilador / Barra", Magnitud = "Tiempo", Metodo = "ANSI/ESD SP3.3-2016", Frecuencia = "Trimestralmente" } },
        { "Bolsa disipativa", new EsdElementInfo { Limite = "RS < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Plástico disipativo", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.11", Frecuencia = "Semestralmente" } },
        { "Cautín / Estación de soldar", new EsdElementInfo { Limite = "RTG < 10 ohms", RefNum = 10.0, TipoMaterial = "Metal / Punta", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Caja Disipativa", new EsdElementInfo { Limite = "RS < 1.0x10^11 ohms", RefNum = 1.0e11, TipoMaterial = "Plástico / Cartón", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.11", Frecuencia = "Anualmente" } },
        { "Caja conductiva", new EsdElementInfo { Limite = "RS < 1.0x10^4 ohms", RefNum = 1.0e4, TipoMaterial = "Plástico conductivo", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.11", Frecuencia = "Anualmente" } },
        { "Charola conductiva", new EsdElementInfo { Limite = "RS < 1.0x10^4 ohms", RefNum = 1.0e4, TipoMaterial = "Plástico conductivo", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.13/11.11", Frecuencia = "Anualmente" } },
        { "Charola Disipativa", new EsdElementInfo { Limite = "RS < 1.0x10^11 ohms", RefNum = 1.0e11, TipoMaterial = "Plástico disipativo", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.13/11.11", Frecuencia = "Anualmente" } },
        { "Magazine", new EsdElementInfo { Limite = "RS < 1.0x10^11 ohms", RefNum = 1.0e11, TipoMaterial = "Metal / Plástico", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM11.13/11.11", Frecuencia = "Anualmente" } },
        { "Bata", new EsdElementInfo { Limite = "RPP < 1.0x10^11 ohms", RefNum = 1.0e11, TipoMaterial = "Tela ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Gorra", new EsdElementInfo { Limite = "RPP < 1.0x10^11 ohms", RefNum = 1.0e11, TipoMaterial = "Tela ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Rack", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Metal", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM4.1", Frecuencia = "Anualmente" } },
        { "Carrito", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Metal", Magnitud = "Resistencia", Metodo = "ANSI/ESD STM4.1", Frecuencia = "Anualmente" } },
        { "Silla ESD", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Tela / Vinil ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Guantes Nitrilo", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Nitrilo", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Guantes Tela", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Tela ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Tapete de piso", new EsdElementInfo { Limite = "RTG < 1.0x10^9 ohms", RefNum = 1.0e9, TipoMaterial = "Caucho / Vinil ESD", Magnitud = "Resistencia", Metodo = "ANSI/ESD TR53", Frecuencia = "Semestralmente" } },
        { "Aislantes - EPA (General)", new EsdElementInfo { Limite = ">30 cm de ESDS", RefNum = 2000.0, TipoMaterial = "Material Aislante", Magnitud = "Voltaje", Metodo = "Anexo A.2", Frecuencia = "Semestralmente" } },
        { "Aislantes - Conductores Aislados", new EsdElementInfo { Limite = "< 35 Volts", RefNum = 35.0, TipoMaterial = "Conductor Aislado", Magnitud = "Voltaje", Metodo = "Anexo A.2", Frecuencia = "Semestralmente" } },
        { "Aislantes - Contacto directo", new EsdElementInfo { Limite = "<= 125 Volts/in", RefNum = 125.0, TipoMaterial = "Material Aislante", Magnitud = "Voltaje", Metodo = "Anexo A.2", Frecuencia = "Semestralmente" } },
        { "Bolsas blindadas", new EsdElementInfo { Limite = "Visual", RefNum = 0.0, TipoMaterial = "Plástico metalizado", Magnitud = "Otro", Metodo = "Inspección visual", Frecuencia = "Trimestralmente" } }
    };

    public static readonly Dictionary<string, string> MapaUnidades = new()
    {
        { "Resistencia", "Ohms" },
        { "Voltaje", "Volts" },
        { "Tiempo", "Segundos" },
        { "Longitud", "cm" },
        { "Otro", "N/A" }
    };

    public static readonly Dictionary<string, int> HierarchyRoles = new()
    {
        { "SuperAdmin", 100 },
        { "admin", 100 },
        { "CompanyAdmin", 80 },
        { "ADMIN", 60 },
        { "SiteAdmin", 60 },
        { "SUPERVISOR", 40 },
        { "AUDITOR", 20 },
        { "READONLY", 10 },
        { "USER", 10 }
    };
}
