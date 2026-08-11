namespace SiteQueryDefectTracking.Infrastructure.Persistence;

public record AccessoryItem(string Code, string Name, string Category, string Specification, string Material, string Unit = "");

public static class AccessoriesCatalogue
{
    public static readonly IReadOnlyList<AccessoryItem> All = Build();

    private static List<AccessoryItem> Build() => new()
    {
        // ---------- SNAP TIE ----------
        new("DAAA", "Snap Tie 4'", "Snap Tie", "Wall thickness (mm)", "Steel", "nos"),
        new("DABA", "2-Hole Reusable Tie", "Snap Tie", "Wall thickness (mm)", "Steel", "nos"),
        new("DACA", "3-Hole Reusable Tie (W37)", "Snap Tie", "Wall thickness (mm)", "Steel", "nos"),
        new("DAHA", "3-Hole Reusable Tie (W33)", "Snap Tie", "Wall thickness (mm)", "Steel", "nos"),

        // ---------- RE-CONE TIE ----------
        new("DTGD", "Re-cone Tie", "Re-cone Tie", "[1/2] - Wall thickness (mm)", "Steel + PVC", "nos"),

        // ---------- T-TIE ----------
        new("DADA", "T-Tie", "T-Tie", "Wall thickness (mm)", "Steel", "nos"),

        // ---------- DOUBLE POUR TIE ----------
        new("DAFA", "Double Pour Tie", "Double Pour Tie", "Wall th'k - Wall space distance", "Steel", "nos"),

        // ---------- AL-ROD TIE / TIE ROD ----------
        new("DAGA", "Al-Rod Tie", "Tie Rod", "Wall thickness (mm)", "Steel", "nos"),
        new("DAGB", "Tie Rod (1/2)", "Tie Rod", "Length", "Steel", "nos"),
        new("DAGC", "Tie Rod (5/8)", "Tie Rod", "Length", "Steel", "nos"),

        // ---------- SEPA BOLT ----------
        new("DAIB", "Sepa Bolt (1/2)", "Sepa Bolt", "Wall thickness (mm)", "Steel", "nos"),
        new("DAIC", "Sepa Bolt (5/8)", "Sepa Bolt", "Wall thickness (mm)", "Steel", "nos"),

        // ---------- PROP / SUPPORT ----------
        new("DRVA0001", "Support (V1)", "Support", "3.1 m extended", "Steel", "nos"),
        new("DRVA0002", "Support (V2)", "Support", "Extended", "Steel", "nos"),
        new("DRWA0001", "Support (V3)", "Support", "Extended", "Steel", "nos"),
        new("DRWA0002", "Support (V4)", "Support", "Extended", "Steel", "nos"),
        new("DRTA0005", "Pipe Head Adaptor", "Support", "Pipe Dia.", "Steel", "nos"),

        // ---------- D-CONE ----------
        new("DBAA0000", "D-Cone", "D-Cone", "[1/2] - 40MM / [5/8] - 60MM", "Steel + PVC", "nos"),

        // ---------- PIN (KK / ALFA / AO / ALFU) ----------
        new("DCAA0001", "Pin (KK-Type)", "Pin", "KK", "Steel", "nos"),
        new("DCAA0015", "Pin (ALFA-Type)", "Pin", "ASIA", "Steel", "nos"),
        new("DCAB0059", "Pin (AO-Type)", "Pin", "A-ONE", "Steel", "nos"),
        new("DCAC0059", "Pin (ALFU-Type)", "Pin", "USA", "Steel", "nos"),

        // ---------- LONG PIN ----------
        new("DCBA0064", "Long Pin 64L", "Long Pin", "ALF - Form Clip", "Steel", "nos"),
        new("DCBB0100", "Long Pin 100L", "Long Pin", "HD - 100L", "Steel", "nos"),
        new("DCBB0150", "Long Pin 150L", "Long Pin", "SM - 150L", "Steel", "nos"),
        new("DCBB0152", "Long Pin 152L", "Long Pin", "KK - 152L", "Steel", "nos"),
        new("DCBC0157", "Long Pin 157L", "Long Pin", "ALF - Pin", "Steel", "nos"),

        // ---------- WEDGE ----------
        new("DCCA0001", "Wedge (ALFA-Type)", "Wedge", "ASIA", "Steel", "nos"),
        new("DCCB0001", "Wedge (AO-Type)", "Wedge", "A-ONE", "Steel", "nos"),
        new("DCCC0001", "Straight Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos"),
        new("DCCD0001", "5 Degree Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos"),
        new("DCCE0001", "Curved Wedge (ALFU-Type)", "Wedge", "USA", "Steel", "nos"),

        // ---------- WALER BRACKET ----------
        new("DDAA0001", "Adjustable Waler Bracket (ALFA-Type)", "Waler Bracket", "50x50", "Steel", "nos"),
        new("DDAA0003", "Adjustable Waler Bracket (ALFU-Type)", "Waler Bracket", "2x4", "Steel", "nos"),
        new("DDBA0001", "Std. Waler (ALFU-Type)", "Waler Bracket", "2x4", "Steel", "nos"),

        // ---------- WALER BOARD ----------
        new("DRMA", "Waler Board", "Waler Board", "50x50x3.2t - Length (M)", "Steel", "m"),

        // ---------- KL BRACKET ----------
        new("DDCA0099", "KL Bracket \"U\" Type - 99.2MM", "KL Bracket", "U-99.2MM", "Steel", "nos"),
        new("DDCB0099", "KL Bracket \"Z\" Type - 99.2MM", "KL Bracket", "Z-99.2MM", "Steel", "nos"),
        new("DDCE0092", "KL Bracket \"U\" Type - 92.5MM", "KL Bracket", "U-92.5MM", "Steel", "nos"),
        new("DDCF0092", "KL Bracket \"Z\" Type - 92.5MM", "KL Bracket", "Z-92.5MM", "Steel", "nos"),

        // ---------- WALL BRACKET ----------
        new("DEAA0600", "Std. Wall Bracket (DYVIDAG-Type)", "Wall Bracket", "1150X1000X600", "Steel", "nos"),
        new("DEAA0740", "Wall Bracket (TIE-Type)", "Wall Bracket", "1070X950X740", "Steel", "nos"),
        new("DEBA1000", "Slab Bracket", "Wall Bracket", "1150X1000", "Steel", "nos"),
        new("DECA0245", "Special Wall Bracket", "Wall Bracket", "1150X1000X245", "Steel", "nos"),

        // ---------- KICKER ANCHOR ----------
        new("DFAB1600", "Kicker Anchor Nut", "Kicker Anchor", "M16 x 2.0", "Steel", "nos"),
        new("DFAB1601", "Kicker Anchor Washer", "Kicker Anchor", "M16", "Steel", "nos"),
        new("DFAB1610", "Anchor Sleeve 100MM", "Kicker Anchor", "100MM", "PVC", "nos"),
        new("DFAB1675", "Kicker Anchor Bolt", "Kicker Anchor", "M16x75L", "Steel", "nos"),

        // ---------- DYVIDAG / PANEL JOIN ----------
        new("DFAC1610", "DYVIDAG Kicker Anchor Bolt", "DYVIDAG Bolt", "100mm", "Steel", "nos"),
        new("DFAC1611", "DYVIDAG Kicker Anchor Al-Nut", "DYVIDAG Bolt", "Al-Nut", "Aluminum", "nos"),
        new("DFAC1635", "Panel Join Bolt", "DYVIDAG Bolt", "M16x35", "Steel", "nos"),
        new("DFAC1636", "Panel Join Nut", "DYVIDAG Bolt", "M16", "Steel", "nos"),
        new("DFAE", "DYVIDAG Bolt", "DYVIDAG Bolt", "17Ø x Length", "Steel", "nos"),

        // ---------- WALER FIXING BOLT ----------
        new("DFAF0150", "Waler Fixing Bolt (HEX Bolt-Type)", "Waler Fixing Bolt", "M16*35 - Length", "Steel", "nos"),
        new("DFAG0200", "Waler Fixing Bolt (Pin-Type) - 5/8", "Waler Fixing Bolt", "Length", "Steel", "nos"),
        new("DFAH2012", "Waler Fixing Bolt (Pin-Type) - 1/2", "Waler Fixing Bolt", "Length", "Steel", "nos"),

        // ---------- WING NUT ----------
        new("DHAA0001", "Wing Nut 1/2", "Wing Nut", "1/2\"", "Cast-iron", "nos"),
        new("DHBA0001", "Wing Nut 5/8", "Wing Nut", "5/8\"", "Cast-iron", "nos"),

        // ---------- BRACKET BOLT ----------
        new("DFAA", "Bracket Bolt", "Bracket Bolt", "17Ø x Length", "Steel", "nos"),

        // ---------- FORM CLIP ----------
        new("DIAA0001", "Form Clip-LH (ALFA-Type)", "Form Clip", "LH (Asia)", "Steel", "nos"),
        new("DIAB0001", "Form Clip-RH (ALFA-Type)", "Form Clip", "RH (Asia)", "Steel", "nos"),
        new("DIBA0001", "Form Clip-LH (ALFU-Type)", "Form Clip", "LH (USA)", "Steel", "nos"),
        new("DIBB0001", "Form Clip-RH (ALFU-Type)", "Form Clip", "RH (USA)", "Steel", "nos"),

        // ---------- PIN LOCK ----------
        new("DJAC0001", "Pin Lock PVC Cylinder", "Pin Lock", "PVC Cylinder", "PVC", "nos"),
        new("DJBA0001", "Pin Lock LH-16.5 (Wall)", "Pin Lock", "LH (Asia)", "Steel + PVC", "nos"),
        new("DJBB0001", "Pin Lock RH-16.5 (Wall)", "Pin Lock", "RH (Asia)", "Steel + PVC", "nos"),

        // ---------- PVC TIE SLEEVE / PVC PIPE ----------
        new("DKAA", "PVC Tie Sleeve", "PVC Sleeve", "Wall thickness (mm)", "PVC", "nos"),
        new("DLAA0000", "PVC Pipe 22Ø, 2M", "PVC Pipe", "22Ø / 2M", "PVC", "m"),
        new("DLAA0002", "PVC Pipe [1/2, 2M]", "PVC Pipe", "[1/2 - 2M]", "PVC", "m"),
        new("DLAA0003", "PVC Pipe [5/8, 2M]", "PVC Pipe", "[5/8 - 2M]", "PVC", "m"),

        // ---------- DOOR BRACE ----------
        new("DQAA04000900", "Door Brace 400~900", "Door Brace", "400~900", "Steel", "nos"),
        new("DQAA05000700", "Door Brace 500~700", "Door Brace", "600", "Steel", "nos"),
        new("DQAA06000800", "Door Brace 600~800", "Door Brace", "600~800", "Steel", "nos"),
        new("DQAA07000900", "Door Brace 700~900", "Door Brace", "700~900", "Steel", "nos"),
        new("DQAA07001100", "Door Brace 700~1100", "Door Brace", "700~1100", "Steel", "nos"),
        new("DQAA07500950", "Door Brace 750~950", "Door Brace", "750~950", "Steel", "nos"),
        new("DQAA09001100", "Door Brace 900~1100", "Door Brace", "900~1100", "Steel", "nos"),
        new("DQAA09001600", "Door Brace 900~1600", "Door Brace", "900~1600", "Steel", "nos"),
        new("DQAA09501100", "Door Brace 950~1100", "Door Brace", "950~1100", "Steel", "nos"),
        new("DQAA10501200", "Door Brace 1050~1200", "Door Brace", "1050~1200", "Steel", "nos"),
        new("DQAA11001300", "Door Brace 1100~1300", "Door Brace", "1100~1300", "Steel", "nos"),
        new("DQAA11501300", "Door Brace 1150~1300", "Door Brace", "1150~1300", "Steel", "nos"),
        new("DQAA12001400", "Door Brace 1200~1400", "Door Brace", "1200~1400", "Steel", "nos"),
        new("DQAA14001600", "Door Brace 1400~1600", "Door Brace", "1400~1600", "Steel", "nos"),
        new("DQAA16001800", "Door Brace 1600~1800", "Door Brace", "1600~1800", "Steel", "nos"),
        new("DQAA18002000", "Door Brace 1800~2000", "Door Brace", "1800~2000", "Steel", "nos"),

        // ---------- LOW CONTROL BRACE ----------
        new("DEDA0001", "Low Control Brace", "Low Control Brace", "600L", "Steel", "nos"),

        // ---------- PLUMBING WALL BRACE ----------
        new("DQAE2000", "Plumbing Wall Brace 2000 [2400H]", "Plumbing Wall Brace", "2000 [2400H]", "Steel", "nos"),
        new("DQAE2200", "Plumbing Wall Brace 2200 [3000H]", "Plumbing Wall Brace", "2200 [3000H]", "Steel", "nos"),
        new("DQAE2700", "Plumbing Wall Brace 2700 [3500H]", "Plumbing Wall Brace", "2700 [3500H]", "Steel", "nos"),
        new("DQAE2800", "Plumbing Wall Brace 2800 [3500H]", "Plumbing Wall Brace", "2800 [3500H]", "Steel", "nos"),
        new("DQAG3000", "Plumbing Wall Brace 3000", "Plumbing Wall Brace", "3000", "Steel", "nos"),

        // ---------- PUSH-PULL BRACING / CAP BRACES ----------
        new("DZAA", "Push-Pull Bracing Set", "Push-Pull Bracing", "Long 1800L & Short 800L", "Steel", "nos"),
        new("DQAB0001", "Cap Braces (ALFU-Type)", "Cap Brace", "STD (USA)", "Steel", "nos"),
        new("DQAB0700", "Cap Braces (Special)", "Cap Brace", "Special (700)", "Steel", "nos"),
        new("DQAF0600", "Cap Braces (ALFA-Type)", "Cap Brace", "STD (Asia)", "Steel", "nos"),

        // ---------- TIE KEEPER ----------
        new("DPAA0001", "Tie Keeper (Omniwedge)", "Tie Keeper", "Omniwedge", "Steel", "nos"),

        // ---------- TOOLS & ETC ----------
        new("DRAA1710", "Bracket Flange Nut", "Tools", "17-100Ø", "Cast-iron", "nos"),
        new("DRBA0001", "Tie Puller", "Tools", "Standard", "Steel", "nos"),
        new("DRAA0001", "Pin Lock Stripping Tool", "Tools", "Standard", "Cast-iron", "nos"),
        new("DRCA0002", "Panel Puller", "Tools", "Y style", "Steel", "nos"),
        new("DRDA0001", "Hole Aligner", "Tools", "Standard", "Steel", "nos"),
        new("DRFA0001", "Tie Breaker Bar", "Tools", "Standard", "Steel", "nos"),
        new("DRGA0001", "Sleeve Eject Bar", "Tools", "Standard", "Steel", "nos"),
        new("DRNA0002", "Work Bench (1000H)", "Tools", "1200x500x1000 (H)", "Steel", "nos"),
        new("DRNA0004", "Work Bench (750H)", "Tools", "1200X500X750 (H)", "Steel", "nos"),
        new("DROB0001", "Wire Turnbuckle", "Tools", "5/8*6M", "Steel", "nos"),

        // ---------- FITTINGS ----------
        new("DTGA0001", "PVC Cone", "Fittings", "Standard", "PVC", "nos"),
        new("DUAA0001", "Square Washer", "Fittings", "Standard", "Steel", "nos"),
        new("DZAA0004", "Double Waler Nut Clamp", "Fittings", "Standard", "Steel", "nos"),
        new("DZAA0005", "Double Waler Clamp Washer", "Fittings", "130X50", "Steel", "nos"),
        new("DZAA0006", "Plastic Cap 16Ø", "Fittings", "16Ø", "PVC", "nos"),
        new("DZAA0008", "Plastic Cap 18Ø", "Fittings", "18Ø", "PVC", "nos")
    };
}