namespace AllOnShelves
{
    /// <summary>
    /// Ремонт-«лего» (23.09.2026): голый магазин района и улучшения, вырезанные из одного
    /// и того же кадра, поэтому ложатся на него пиксель в пиксель.
    /// Файл создаёт Tools/ArtPipeline/lego_build.py — руками не править.
    /// </summary>
    public static class RenoLegoData
    {
        public struct Piece
        {
            public string Sprite;
            public float X, Y, W, H;
            public bool Fly;      // слой годится для прилёта (не размазан по кадру)
        }

        static readonly Piece[] D01 = { new Piece { Sprite = "meta_lego_d01_i1", X = -200.0f, Y = -283.0f, W = 900.0f, H = 228.0f, Fly = true }, new Piece { Sprite = "meta_lego_d01_i2", X = -28.0f, Y = 6.9f, W = 1242.0f, H = 834.6f, Fly = false }, new Piece { Sprite = "meta_lego_d01_i3", X = -18.0f, Y = -240.1f, W = 1260.0f, H = 330.2f, Fly = false }, new Piece { Sprite = "meta_lego_d01_i4", X = -18.0f, Y = -9.5f, W = 1260f, H = 787f, Fly = false }, new Piece { Sprite = "meta_lego_d01_i5", X = -17.5f, Y = 8.9f, W = 1257.0f, H = 820.2f, Fly = false } };
        static readonly float[] B01 = { -613f, -405f, 614f, 421f };
        static readonly Piece[] D02 = { new Piece { Sprite = "meta_lego_d02_i1", X = 224.5f, Y = -226.5f, W = 1455f, H = 261f, Fly = false }, new Piece { Sprite = "meta_lego_d02_i2", X = 200.0f, Y = -40.7f, W = 1464.0f, H = 635.4f, Fly = false }, new Piece { Sprite = "meta_lego_d02_i3", X = -199.5f, Y = -128.7f, W = 1427.0f, H = 417.4f, Fly = false }, new Piece { Sprite = "meta_lego_d02_i4", X = 31.5f, Y = -5.0f, W = 1099.0f, H = 670.2f, Fly = true }, new Piece { Sprite = "meta_lego_d02_i5", X = 201.5f, Y = -61.7f, W = 1485.0f, H = 557.5f, Fly = false }, new Piece { Sprite = "meta_lego_d02_i6", X = 19.5f, Y = 48.1f, W = 1875.0f, H = 805.8f, Fly = false } };
        static readonly float[] B02 = { -532f, -336f, 514f, 331f };
        static readonly Piece[] D03 = { new Piece { Sprite = "meta_lego_d03_i1", X = 453.0f, Y = 18.0f, W = 308f, H = 268f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i2", X = -21.0f, Y = -272.0f, W = 372.0f, H = 232.0f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i3", X = -865.0f, Y = 46.0f, W = 76.0f, H = 404.0f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i4", X = 625.0f, Y = -242.0f, W = 668.0f, H = 320.0f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i5", X = 161.0f, Y = -180.0f, W = 368.0f, H = 164.0f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i6", X = -275.0f, Y = -106.0f, W = 380.0f, H = 316.0f, Fly = true }, new Piece { Sprite = "meta_lego_d03_i7", X = -238.0f, Y = 291.0f, W = 660.0f, H = 208.0f, Fly = true } };
        static readonly Piece[] D04 = { new Piece { Sprite = "meta_lego_d04_i1", X = -514.0f, Y = 332.0f, W = 192.0f, H = 88.0f, Fly = true }, new Piece { Sprite = "meta_lego_d04_i2", X = 422.0f, Y = -326.0f, W = 232.0f, H = 120.0f, Fly = true }, new Piece { Sprite = "meta_lego_d04_i3", X = 0.0f, Y = -15.4f, W = 1914.0f, H = 1046.9f, Fly = false }, new Piece { Sprite = "meta_lego_d04_i4", X = 698.0f, Y = -20.0f, W = 520.0f, H = 468.0f, Fly = true }, new Piece { Sprite = "meta_lego_d04_i5", X = -671.0f, Y = -98.0f, W = 572.0f, H = 292.0f, Fly = true }, new Piece { Sprite = "meta_lego_d04_i6", X = -9.0f, Y = 52.0f, W = 796.0f, H = 216.0f, Fly = true }, new Piece { Sprite = "meta_lego_d04_i7", X = -9.0f, Y = 325.0f, W = 580.0f, H = 344.0f, Fly = true } };
        static readonly Piece[] D05 = { new Piece { Sprite = "meta_lego_d05_i1", X = 162.5f, Y = 204.5f, W = 1129.0f, H = 375.2f, Fly = true }, new Piece { Sprite = "meta_lego_d05_i2", X = 127.5f, Y = 19.4f, W = 1189.0f, H = 775.2f, Fly = false }, new Piece { Sprite = "meta_lego_d05_i3", X = -352.0f, Y = -98.0f, W = 156.0f, H = 144.0f, Fly = true }, new Piece { Sprite = "meta_lego_d05_i4", X = 246.5f, Y = 14.5f, W = 1351f, H = 755f, Fly = false }, new Piece { Sprite = "meta_lego_d05_i5", X = 162.0f, Y = 23.5f, W = 1520.0f, H = 766.0f, Fly = false }, new Piece { Sprite = "meta_lego_d05_i6", X = 1.5f, Y = 23.5f, W = 1837f, H = 761f, Fly = false }, new Piece { Sprite = "meta_lego_d05_i7", X = 1.0f, Y = 20.1f, W = 1836.0f, H = 759.8f, Fly = false }, new Piece { Sprite = "meta_lego_d05_i8", X = -3.0f, Y = 9.5f, W = 1884.0f, H = 772.7f, Fly = false } };
        static readonly float[] B05 = { -608f, -366f, 760f, 408f };
        static readonly Piece[] D06 = { new Piece { Sprite = "meta_lego_d06_i1", X = 622.0f, Y = -151.0f, W = 492.0f, H = 556.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i2", X = 330.0f, Y = -304.0f, W = 952.0f, H = 220.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i3", X = 434.0f, Y = -205.0f, W = 772.0f, H = 416.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i4", X = 582.0f, Y = -289.0f, W = 572.0f, H = 276.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i5", X = 585.0f, Y = -137.0f, W = 560.0f, H = 576.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i6", X = 397.0f, Y = -235.0f, W = 924.0f, H = 376.0f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i7", X = 12.0f, Y = -219.0f, W = 1688.0f, H = 421.3f, Fly = true }, new Piece { Sprite = "meta_lego_d06_i8", X = 24.0f, Y = 19.2f, W = 1720.0f, H = 901.0f, Fly = true } };
        static readonly Piece[] D07 = { new Piece { Sprite = "meta_lego_d07_i1", X = -5.0f, Y = 75.0f, W = 1750.0f, H = 635.4f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i2", X = -559.0f, Y = -99.0f, W = 640.0f, H = 200.0f, Fly = true }, new Piece { Sprite = "meta_lego_d07_i3", X = -201.5f, Y = -121.2f, W = 1359.0f, H = 281.0f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i4", X = 0.0f, Y = -8.0f, W = 1914f, H = 500f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i5", X = 72.0f, Y = 65.2f, W = 1570.0f, H = 650.6f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i6", X = 0.0f, Y = -79.4f, W = 1914.0f, H = 350.9f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i7", X = 0.0f, Y = -5.4f, W = 1914.0f, H = 492.9f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i8", X = 0.0f, Y = 70.6f, W = 1914.0f, H = 642.6f, Fly = false }, new Piece { Sprite = "meta_lego_d07_i9", X = -34.0f, Y = -63.0f, W = 1698.0f, H = 365.3f, Fly = false } };
        static readonly float[] B07 = { -960f, -259f, 960f, 335f };
        static readonly Piece[] D08 = { new Piece { Sprite = "meta_lego_d08_i1", X = 25.0f, Y = 28.0f, W = 696.0f, H = 480.0f, Fly = true }, new Piece { Sprite = "meta_lego_d08_i2", X = -31.0f, Y = -93.0f, W = 456.0f, H = 292.0f, Fly = true }, new Piece { Sprite = "meta_lego_d08_i3", X = 1.5f, Y = 34.0f, W = 1911.0f, H = 843.7f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i4", X = 0.0f, Y = 24.6f, W = 1914.0f, H = 860.6f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i5", X = 0.0f, Y = 24.0f, W = 1914f, H = 852f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i6", X = -8.5f, Y = 27.1f, W = 1897.0f, H = 843.9f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i7", X = 0.0f, Y = 21.6f, W = 1914.0f, H = 852.6f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i8", X = 278.5f, Y = 74.8f, W = 1357.0f, H = 742.3f, Fly = false }, new Piece { Sprite = "meta_lego_d08_i9", X = 0.0f, Y = 23.0f, W = 1914f, H = 844f, Fly = false } };
        static readonly float[] B08 = { -949f, -387f, 960f, 454f };
        static readonly Piece[] D09 = { new Piece { Sprite = "meta_lego_d09_i1", X = 25.0f, Y = -313.0f, W = 984.0f, H = 308.0f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i2", X = 77.5f, Y = -37.5f, W = 1183f, H = 841f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i3", X = -234.0f, Y = -162.0f, W = 872.0f, H = 580.0f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i4", X = 30.0f, Y = -12.5f, W = 1090f, H = 903f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i5", X = 87.0f, Y = 228.0f, W = 928.0f, H = 440.0f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i6", X = -29.0f, Y = -207.7f, W = 1350.0f, H = 512.0f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i7", X = -28.0f, Y = 14.5f, W = 1348f, H = 943f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i8", X = -18.5f, Y = 14.5f, W = 1379f, H = 943f, Fly = true }, new Piece { Sprite = "meta_lego_d09_i9", X = -4.0f, Y = 11.0f, W = 1434.0f, H = 946.8f, Fly = true } };
        static readonly Piece[] D10 = { new Piece { Sprite = "meta_lego_d10_i1", X = -334.0f, Y = -301.0f, W = 724.0f, H = 300.0f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i2", X = -5.5f, Y = -116.7f, W = 1379.0f, H = 690.0f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i3", X = 54.5f, Y = -1.5f, W = 1095f, H = 907f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i4", X = -701.0f, Y = -146.0f, W = 468.0f, H = 532.0f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i5", X = 442.0f, Y = 44.0f, W = 296.0f, H = 468.0f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i6", X = -229.0f, Y = -270.0f, W = 800.0f, H = 356.0f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i7", X = 65.5f, Y = 78.5f, W = 1065.0f, H = 765.1f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i8", X = -131.0f, Y = 6.0f, W = 1126.0f, H = 910.3f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i9", X = -116.0f, Y = 4.7f, W = 1636.0f, H = 907.8f, Fly = true }, new Piece { Sprite = "meta_lego_d10_i10", X = 0.0f, Y = 3.0f, W = 1454.0f, H = 902.8f, Fly = true } };
        static readonly Piece[] D11 = { new Piece { Sprite = "meta_lego_d11_i1", X = -57.0f, Y = -75.5f, W = 1224.0f, H = 769.4f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i2", X = 253.0f, Y = -168.0f, W = 692.0f, H = 576.0f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i3", X = -8.5f, Y = -251.6f, W = 1315.0f, H = 385.3f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i4", X = -8.5f, Y = -245.0f, W = 1315f, H = 396f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i5", X = -19.0f, Y = -73.0f, W = 904.0f, H = 748.0f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i6", X = 6.0f, Y = 301.0f, W = 952.0f, H = 248.0f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i7", X = 33.0f, Y = -305.0f, W = 992.0f, H = 280.0f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i8", X = 50.0f, Y = 53.5f, W = 1060f, H = 857f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i9", X = 6.5f, Y = -137.7f, W = 1361.0f, H = 617.3f, Fly = true }, new Piece { Sprite = "meta_lego_d11_i10", X = 0.5f, Y = 14.3f, W = 1389.0f, H = 927.4f, Fly = true } };
        static readonly Piece[] D12 = { new Piece { Sprite = "meta_lego_d12_i1", X = 0.5f, Y = 55.5f, W = 1757.0f, H = 878.4f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i2", X = 1.0f, Y = 55.0f, W = 1850.0f, H = 874.0f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i3", X = 1.0f, Y = 1.1f, W = 1850.0f, H = 787.4f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i4", X = 1.0f, Y = -145.0f, W = 1770.0f, H = 491.5f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i5", X = 0.5f, Y = -135.4f, W = 1751.0f, H = 532.7f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i6", X = -4.5f, Y = 45.6f, W = 1783.0f, H = 898.2f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i7", X = 2.0f, Y = -138.9f, W = 1754.0f, H = 581.7f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i8", X = -26.0f, Y = 51.5f, W = 1862.0f, H = 902.6f, Fly = false }, new Piece { Sprite = "meta_lego_d12_i9", X = 0.0f, Y = 24.1f, W = 1914.0f, H = 957.6f, Fly = false } };
        static readonly float[] B12 = { -893f, -376f, 894f, 490f };
        static readonly Piece[] D13 = { new Piece { Sprite = "meta_lego_d13_i1", X = -9.0f, Y = -133.9f, W = 1776.0f, H = 589.2f, Fly = false }, new Piece { Sprite = "meta_lego_d13_i2", X = 78.5f, Y = -131.0f, W = 1739.0f, H = 591.4f, Fly = false }, new Piece { Sprite = "meta_lego_d13_i3", X = 87.0f, Y = -123.9f, W = 1740.0f, H = 625.7f, Fly = false }, new Piece { Sprite = "meta_lego_d13_i4", X = 24.5f, Y = -157.3f, W = 1663.0f, H = 558.6f, Fly = false }, new Piece { Sprite = "meta_lego_d13_i5", X = 45.0f, Y = -221.5f, W = 1670.0f, H = 430.3f, Fly = false }, new Piece { Sprite = "meta_lego_d13_i6", X = -482.0f, Y = -301.0f, W = 620.0f, H = 260.0f, Fly = true }, new Piece { Sprite = "meta_lego_d13_i7", X = -479.0f, Y = -303.0f, W = 632.0f, H = 256.0f, Fly = true }, new Piece { Sprite = "meta_lego_d13_i8", X = -573.0f, Y = -315.0f, W = 420.0f, H = 224.0f, Fly = true }, new Piece { Sprite = "meta_lego_d13_i9", X = 0.0f, Y = 30.0f, W = 1914.0f, H = 941.7f, Fly = false } };
        static readonly float[] B13 = { -953f, -413f, 953f, 494f };
        static readonly Piece[] D14 = { new Piece { Sprite = "meta_lego_d14_i1", X = 0.5f, Y = 23.6f, W = 1885.0f, H = 1008.5f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i2", X = 0.5f, Y = -95.9f, W = 1885.0f, H = 787.5f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i3", X = 0.5f, Y = -52.9f, W = 1887.0f, H = 877.5f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i4", X = 0.5f, Y = -67.9f, W = 1887.0f, H = 847.5f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i5", X = 0.5f, Y = -43.9f, W = 1885.0f, H = 853.8f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i6", X = -0.5f, Y = -142.8f, W = 1701.0f, H = 705.0f, Fly = false }, new Piece { Sprite = "meta_lego_d14_i7", X = -852.0f, Y = -8.0f, W = 212.0f, H = 644.0f, Fly = true }, new Piece { Sprite = "meta_lego_d14_i8", X = 216.5f, Y = -148.5f, W = 1141.0f, H = 749.2f, Fly = true }, new Piece { Sprite = "meta_lego_d14_i9", X = -2.0f, Y = 0.5f, W = 1910.0f, H = 1052.7f, Fly = false } };
        static readonly float[] B14 = { -940f, -469f, 940f, 522f };
        static readonly Piece[] D15 = { new Piece { Sprite = "meta_lego_d15_i1", X = 2.5f, Y = 3.5f, W = 1903.0f, H = 1070.7f, Fly = false }, new Piece { Sprite = "meta_lego_d15_i2", X = -1.5f, Y = -12.4f, W = 1911.0f, H = 1052.9f, Fly = false }, new Piece { Sprite = "meta_lego_d15_i3", X = 2.5f, Y = -56.5f, W = 1901.0f, H = 964.7f, Fly = false }, new Piece { Sprite = "meta_lego_d15_i4", X = 2.0f, Y = 3.5f, W = 1910.0f, H = 1067.0f, Fly = false }, new Piece { Sprite = "meta_lego_d15_i5", X = -353.5f, Y = -319.1f, W = 1153.0f, H = 428.1f, Fly = false }, new Piece { Sprite = "meta_lego_d15_i6", X = -605.0f, Y = -292.0f, W = 552.0f, H = 416.0f, Fly = true }, new Piece { Sprite = "meta_lego_d15_i7", X = 625.0f, Y = 21.0f, W = 652.0f, H = 936.0f, Fly = true }, new Piece { Sprite = "meta_lego_d15_i8", X = 631.0f, Y = 35.0f, W = 656.0f, H = 968.0f, Fly = true }, new Piece { Sprite = "meta_lego_d15_i9", X = 0.0f, Y = -0.9f, W = 1914.0f, H = 1075.9f, Fly = false } };
        static readonly float[] B15 = { -947f, -527f, 946f, 337f };

        public static readonly int[] Districts = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        /// <summary>Улучшения района в порядке покупки (пусто — у района нет «лего»-серии).</summary>
        public static Piece[] Pieces(int district)
        {
            switch (district)
            {
                case 1: return D01;
                case 2: return D02;
                case 3: return D03;
                case 4: return D04;
                case 5: return D05;
                case 6: return D06;
                case 7: return D07;
                case 8: return D08;
                case 9: return D09;
                case 10: return D10;
                case 11: return D11;
                case 12: return D12;
                case 13: return D13;
                case 14: return D14;
                case 15: return D15;
                default: return null;
            }
        }

        public static bool Has(int district) => Pieces(district) != null;

        /// <summary>
        /// Границы вырезанного магазина в кадре 1920×1080 (x0, y0, x1, y1; центр — 0, вверх — плюс).
        /// null — магазин нарисован вместе с улицей, вписывать его некуда.
        /// </summary>
        public static float[] Bounds(int district)
        {
            switch (district)
            {
                case 1: return B01;
                case 2: return B02;
                case 5: return B05;
                case 7: return B07;
                case 8: return B08;
                case 12: return B12;
                case 13: return B13;
                case 14: return B14;
                case 15: return B15;
                default: return null;
            }
        }

        /// <summary>Картинка голого магазина района.</summary>
        public static string Base(int district) => Has(district) ? $"meta_lego_d{district:00}_base" : null;
    }
}
