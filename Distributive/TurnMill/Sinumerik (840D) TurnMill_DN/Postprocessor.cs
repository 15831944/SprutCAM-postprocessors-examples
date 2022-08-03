using System.Collections;
namespace SprutTechnology.SCPostprocessor
{
    enum OpType {
        Unknown,
        Mill,
        Lathe,
        Auxiliary,
        WireEDM
    }

    public partial class NCFile: TTextNCFile
    {
        ///<summary>Main nc-programm number</summary>
        public int ProgNumber {get; set;}

        public double LastC = 99999;

        public override void OnInit()
        {
        //     this.TextEncoding = Encoding.GetEncoding("windows-1251");
        }

        public void OutWithN(params string[] s) {
            string outS = "";
            if (!BlockN.Disabled) {
                outS = BlockN.ToString(BlockN);
                BlockN.v = BlockN + 1;
            }
            for (int i=0; i<s.Length; i++) {
                if (!String.IsNullOrEmpty(outS)) 
                   outS += Block.WordsSeparator;
                outS += s[i];
            }
            WriteLine(outS);
        }
    }

    public partial class Postprocessor: TPostprocessor
    {
        #region Common variables definition

        ///<summary>Current nc-file. It could be main or sub.</summary>
        public NCFile nc;

        private CycleSinumerik840D cycle;

        ///<summary>X axis scale coefficient (1 - radial, 2 - diametral)</summary>
        double xScale = 1.0;

        ///<summary>Type of current operation (mill, lathe, etc.)</summary>
        OpType currentOperationType = OpType.Unknown;

        ///<summary>Type of active lathe spindle (main, counter)</summary>
        int activeLatheSpindle;

        double IsFirstC = 1;

        ///<summary>Variable of active plane from LOADTL</summary>
        double Plane_;
        
        public TInp3DPoint FromP_ {get; set;}

        ///<summary>Current point (X, Y, Z coordinates)</summary>
        public TInp3DPoint PT_ {get; set;}
        
        ///<summary>Polar interpolation (setted in the INTERPOLATION command)
        /// 1 - on; 0 - off</summary>
        public int PolarInterp;

        double PPFunFeed;

        #endregion

        void PrintAllTools()
        {
            SortedList tools = new SortedList();
            for (int i=0; i<CLDProject.Operations.Count; i++){
                var op = CLDProject.Operations[i];
                if (op.Tool==null || op.Tool.Command==null)
                    continue;
                if (!tools.ContainsKey(op.Tool.Number))
                    tools.Add(op.Tool.Number, Transliterate(op.Tool.Caption));
            }            
            nc.WriteLine("( Tools list )");
            NumericNCWord toolNum = new NumericNCWord("T{0000}", 0);
            for (int i=0; i<tools.Count; i++){
                toolNum.v = Convert.ToInt32(tools.GetKey(i));
                nc.WriteLine(String.Format("( {0}    {1} )", toolNum.ToString(), tools.GetByIndex(i)));
            }
        }

        public override void OnStartProject(ICLDProject prj)
        {
            nc = new NCFile();
            cycle = new CycleSinumerik840D(this, nc);
            
            nc.OutputFileName = Settings.Params.Str["OutFiles.NCFileName"];
            nc.ProgNumber = Settings.Params.Int["OutFiles.NCProgNumber"];

            nc.WriteLine("%");
            nc.WriteLine("O" + Str(nc.ProgNumber));

            PrintAllTools();
        }

        public override void OnFinishProject(ICLDProject prj)
        {
            nc.Block.Out();
            nc.M.Show(30);
            nc.Block.Out();

            //NCSub.Output ! Выводим все неоттранслированные ранее подпрограммы
            //cldfile translate
        }

        public override void OnStartTechOperation(ICLDTechOperation op, ICLDPPFunCommand cmd, CLDArray cld)
        {
            // One empty line between operations 
            nc.WriteLine();
            
            currentOperationType = (OpType)(int)cld[60];
            xScale = currentOperationType == OpType.Lathe ? 2 : 1;
        }  

        public override void OnComment(ICLDCommentCommand cmd, CLDArray cld)
        {
            nc.WriteLine("( " + cmd.CLDataS + " )");
        }

        public override void OnOrigin(ICLDOriginCommand cmd, CLDArray cld)
        {
            if (cld[4] == 0) nc.GWCS.v = cmd.CSNumber;
            else if(cld[4] == 1079)
            {
                nc.Block.Show(nc.BlockN, nc.GWCS);
                nc.Block.Out();

                if(Abs(cmd.EN.A) > 0.0001 | Abs(cmd.EN.B) > 0.0001 | Abs(cmd.EN.C) > 0.0001)
                {
                    nc.RotA.v = cmd.EN.A; nc.RotA.v0 = 0;
                    nc.RotB.v = cmd.EN.B; nc.RotB.v0 = 0;
                    nc.RotC.v = cmd.EN.C; nc.RotC.v0 = 0;
                    nc.Block.Form();
                }
                nc.X.v = cmd.EP.X; nc.X.v0 = 0;
                nc.Y.v = cmd.EP.Y; nc.Y.v0 = 0; 
                nc.Z.v = cmd.EP.Z; nc.Z.v0 = 0;
                nc.Block.Form();
            }
            else Debug.Write("Unknown coordinate system");
        }

        //ORIGIN's part
        public override void OnWorkpieceCS(ICLDOriginCommand cmd, CLDArray cld)
        {
            base.OnWorkpieceCS(cmd, cld);
        }
        
        // GPlane, Переключение рабочих плоскостей (XY, XZ, YZ)
        private double ChangeGPlane(double cld14) => cld14 switch
        {
            33 => 17,
            41 => 18,
            37 => 19,
            133 => -17,
            141 => -18,
            137 => -19,
            _ => 0
        };

        public override void OnLoadTool(ICLDLoadToolCommand cmd, CLDArray cld)
        {
            nc.Block.Show(nc.BlockN, nc.GWCS);
            nc.Block.Out();

            nc.T.v = cmd.TechOperation.Tool.Number;
            nc.Block.Show(nc.BlockN, nc.T);
            nc.Block.Out();

            //Переключение рабочих плоскостей (XY, XZ, YZ)
            var newGplane = ChangeGPlane((double)cmd.CLD[14]);
            if (newGplane != 0) Plane_ = newGplane;
            else Debug.WriteLine("Wrong given a plane of processing");
        }

        public override void OnPlane(ICLDPlaneCommand cmd, CLDArray cld)
        {
            var newGplane = ChangeGPlane((double)cmd.CLD[14]);
            if (newGplane != 0) Plane_ = newGplane;
            else Debug.WriteLine("Wrong given a plane of processing");
        }

        public override void OnCutCom(ICLDCutComCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) 
            {
                if (cmd.IsLeftDirection) nc.GRCompens.v = 41;
                else nc.GRCompens.v = 42;
            } 

            else 
            {
                nc.GRCompens.v = 40;
            }

            nc.GRCompens.Hide();
        }

        //Задаются координаты исходной точки
        public override void OnFrom(ICLDFromCommand cmd, CLDArray cld)
        {
            nc.X.v = currentOperationType == OpType.Lathe ? cld[1] * 2 : cld[1];
            nc.Y.v = cmd.EP.Y;
            nc.Z.v = cmd.EP.Z;

            nc.X.Hide();
            nc.Y.Hide();
            nc.Z.Hide();

            //FROMX_ = X
            //XT_ = X
            FromP_ = new TInp3DPoint(nc.X.v,nc.Y.v,nc.Z.v);
            PT_ = new TInp3DPoint(nc.X.v,nc.Y.v,nc.Z.v);
        }

        private void DetectSpindle(ICLDSelWorkpieceCommand cmd)
        {
            var ts = cmd.CLDataS.ToUpper();
            Debug.WriteLine(ts);
            if (ts.IndexOf("COUNT") > 0 || ts.IndexOf("SUB") > 0 || ts.IndexOf("SECOND") > 0)
            {
                activeLatheSpindle = 2;                    
            }

            else activeLatheSpindle = 1;
        }

        //Выбор активной державки заготовки          
        public override void OnSelWorkpiece(ICLDSelWorkpieceCommand cmd, CLDArray cld)
        {
            DetectSpindle(cmd); //spindle type definition (main, counter)
            currentOperationType = OpType.Lathe; //switching operation type to lathe for correct output
        }

        public override void OnSpindle(ICLDSpindleCommand cmd, CLDArray cld)
        {
            if (cld[1] == 71)
            {
                if (currentOperationType is OpType.Mill)
                {
                    nc.GPlane.v = Plane_; 
                    if (Abs(nc.GPlane.v) == Abs(nc.GPlane.v0)) nc.GPlane.v0 = nc.GPlane.v;
                    nc.Block.Out();

                    if (activeLatheSpindle == 1)
                    {
                        nc.LastC = 0;
                        nc.C.v = 0; //подключение оси  C
                    }
                    else
                    {
                        nc.C2.v = 0;
                    }
                    nc.Block.Out();
                    nc.SetMS.v = 3; //Активация приводного инструмента 
                    nc.Block.Out();

                    //check the spindle rotation mode (RPM or CSS) cld[4].
                    switch (cmd.SpeedMode)
                    {
                        case CLDSpindleSpeedMode.Unknown: 
                            nc.GCssRpm.v = 97;
                            nc.GCssRpm.Show();
                            nc.S3.v = cmd.RPMValue; //Rotation rate
                            nc.MSp3.v = cmd.RPMValue > 0 ? 3 : 4;

                            nc.MSp3.Show();
                            nc.Block.Out();
                            break;
                        case CLDSpindleSpeedMode.CSS:
                            Debug.WriteLine("Режим CSS во фрезерной обработке не реализован");
                            break;
                    }
                }

                else
                {
                    nc.GPlane.v = 18;
                    nc.Block.Out();

                    if (activeLatheSpindle == 1)
                    {
                        nc.SetMS.v = 1;
                    }

                    else if (activeLatheSpindle == 2)
                    {
                        nc.SetMS.v = 2;
                    }
                    nc.Block.Out();

                    switch (cmd.SpeedMode)
                    {
                        case CLDSpindleSpeedMode.Unknown: 
                            nc.GCssRpm.v = 97;
                            nc.GCssRpm.Show();
                            
                            if (activeLatheSpindle == 1)
                            {
                                nc.S.v = cmd.RPMValue;
                                nc.MSp.v = cmd.RPMValue > 0 ? 4 : 3;

                                nc.Block.Show(nc.S, nc.MSp);
                            }

                            else
                            {
                                nc.S2.v = cmd.RPMValue;
                                nc.MSp2.v = cmd.RPMValue > 0 ? 4 : 3;
                                
                                nc.Block.Show(nc.S2, nc.MSp2);
                            }

                            nc.Block.Out();
                            break;
                        case CLDSpindleSpeedMode.CSS:
                            nc.Lims.v = cmd.RPMValue;
                            nc.Lims.Show();
                            nc.Block.Out();

                            nc.GCssRpm.v = 96; //G96 - by default => needs to change the state
                            nc.GCssRpm.Show();

                            if (activeLatheSpindle == 1)
                            {
                                nc.S.v = cmd.CSSValue; //cld[5]
                                nc.MSp.v = cmd.CSSValue > 0 ? 4 : 3;

                                nc.Block.Show(nc.S, nc.MSp);
                            }

                            else
                            {
                                nc.S2.v = cmd.CSSValue;
                                nc.MSp2.v = cmd.CSSValue > 0 ? 4 : 3;

                                nc.Block.Show(nc.S2, nc.MSp2);
                            }
                            nc.Block.Out();
                            break;
                    }
                }
            }

            else if (cld[1] == 72)
            {
                if(currentOperationType == OpType.Mill)
                {
                    nc.SetMS.v = 3;
                    nc.Block.Out();
                }

                else
                {
                    if (activeLatheSpindle == 1)
                    {
                        nc.SetMS.v = 1;
                        nc.Block.Out();
                        nc.MSp.v = 5;
                        nc.MSp.Show();
                    }
                    else
                    {
                        nc.SetMS.v = 2;
                        nc.Block.Out();
                        nc.MSp2.v = 5;
                    }
                }
                nc.Block.Out();
            }

            else if (cld[1] == 246) return; //Spindle Orient
        }

        public override void OnRapid(ICLDRapidCommand cmd, CLDArray cld)
        {
            if (nc.GInterp.v > 0){
                nc.GInterp.v = 0;
            } 
            nc.ThreadStartAngle.Hide();
        }

        public override void OnMultiGoto(ICLDMultiGotoCommand cmd, CLDArray cld)
        {
            if (nc.GInterp.v > 1){
                nc.GInterp.v = 1;
                nc.GInterp.v0 = nc.GInterp.v;
            }
                
            foreach(CLDMultiMotionAxis ax in cmd.Axes) 
            {
                if (ax.ID == "AxisZ2Pos") {
                    nc.Z2.v = ax.Value;
                    if (nc.Z2.v0 != nc.Z2.v) nc.Block.Out();
                }
                else if (ax.IsX) {
                    nc.X.v = ax.Value * xScale;
                }
                    
                else if (ax.IsY) {
                    nc.Y.v = ax.Value;
                    if (currentOperationType == OpType.Lathe) nc.Y.v0 = nc.Y.v;
                }
                else if (ax.IsZ) {
                    nc.Z.v = ax.Value;
                }
                if (currentOperationType != OpType.Lathe)
                {
                    if (ax.IsC)
                    {
                        if (IsFirstC == 1){
                            nc.C.v = ax.Value;
                            IsFirstC = 0;
                        }

                        else{
                            nc.C.v = cmd.Flt["Axes(AxisCPos).Value"];
                            if (Abs(nc.C.v - nc.LastC) < 180){
                                nc.C.v = ax.Value;
                                if(nc.C.v == nc.LastC) nc.C.v = nc.C.v0;
                            }
                            else{
                                nc.C_.v = nc.C.v - nc.LastC;
                                nc.C.v0 = nc.C.v;
                            }
                        }

                        nc.LastC = cmd.Flt["Axes(AxisCPos).Value"];
                    }

                    else if(ax.IsC2) nc.C2.v = ax.Value;
                }
                
                if (ax.ID == "JawDiameter"){
                    nc.MChuck.v = ax.Value > 220 ? 25 : 26;
                }

                else if (ax.ID == "JawDiameter2"){
                    nc.MChuck2.v = ax.Value > 220 ? 25 : 26;
                }
            }

            nc.Block.Out();  
        }

        public override void OnGoto(ICLDGotoCommand cmd, CLDArray cld)
        {
            if (nc.GInterp.v > 1 && nc.GInterp.v < 4) nc.GInterp.v = 1;
            if (nc.GInterp.v == 33) nc.Block.Show(nc.GInterp);
            if (nc.GPolarOrCyl.v == 1)
            {
                nc.X.v = cmd.EP.X * Cos(nc.RotC.v) - cmd.EP.Y * Sin(nc.RotC.v);
                nc.Y.v = cmd.EP.Y * Cos(nc.RotC.v) + cmd.EP.X * Sin(nc.RotC.v);
            }

            else
            {
                nc.X.v = cmd.EP.X * xScale; //xScale = 2
                nc.Y.v = cmd.EP.Y;
            }

            nc.Z.v = cmd.EP.Z;
            if(currentOperationType == OpType.Lathe )
            {
                nc.Y.v0 = nc.Y.v;
            }
            
            if(nc.X.Changed || nc.Y.Changed || nc.Z.Changed ) nc.Block.Out();
            PT_ = cmd.EP; // current coordinates
            //nc.LastP = cmd.EP;
        }

        public override void OnCoolant(ICLDCoolantCommand cmd, CLDArray cld)
        {
            nc.MCoolant.v = cmd.IsOn ? 8 : 9;
        }

        public override void OnFeedrate(ICLDFeedrateCommand cmd, CLDArray cld)
        {
            nc.F.v = cmd.FeedValue;

            if (nc.F.v < 10000)
            {
                nc.GFeed.v = cld[3] == 315 ? 94 : 95;
                nc.GInterp.v = 1;
            }
            else
            {
                nc.GInterp.v = 0;
                nc.F.v0 = nc.F.v;
            }
            nc.ThreadStartAngle.Hide();
        }

        public override void OnCircle(ICLDCircleCommand cmd, CLDArray cld)
        {
            nc.GInterp.v = cmd.R * Sgn(nc.GPlane.v) > 0 ? 3 : 2;

            if (nc.GPolarOrCyl.v == 1)
            {
                nc.X.v = cmd.EP.X * Cos(nc.RotC.v) - cmd.EP.Y * Sin(nc.RotC.v);
                nc.Y.v = cmd.EP.Y * Cos(nc.RotC.v) + cmd.EP.X * Sin(nc.RotC.v);
            }

            else
            {
                nc.X.v = cmd.EP.X * xScale; //xScale = 2
                nc.Y.v = cmd.EP.Y;
            }

            nc.X.Show();
            nc.Y.Show();
            nc.Z.Show(cmd.EP.Z);

            if (currentOperationType == OpType.Lathe) nc.Y.v0 = nc.Y.v; //don't output in lathe 

            if (Abs(nc.GPlane.v) == 17 && PT_.Z == nc.Z.v) nc.Z.v0 = nc.Z.v;
            else if (Abs(nc.GPlane.v) == 17 && PT_.Y == nc.Y.v) nc.Y.v0 = nc.Y.v;
            else if (Abs(nc.GPlane.v) == 17 && PT_.X == nc.X.v) nc.X.v0 = nc.X.v;

            //Если спираль, то выводим Turn (количество оборотов) явно
            if(Abs(nc.GPlane.v) == 17 && PT_.Z != nc.Z.v)
            {
                nc.Turn.Show(0);

                if(PT_.X == nc.X.v && PT_.Y == nc.Y.v){
                    nc.Turn.v = 1;
                }
            }

            else if (Abs(nc.GPlane.v) == 18 && PT_.Y != nc.Y.v)
            {
                nc.Turn.Show(0);

                if(PT_.X == nc.X.v && PT_.Z == nc.Z.v){
                    nc.Turn.v = 1;
                }
            }
            
            else if (Abs(nc.GPlane.v) == 19 && PT_.X != nc.X.v)
            {
                nc.Turn.v = 0;
                nc.Turn.Show(0);

                if(PT_.Y == nc.Y.v && PT_.Z == nc.Z.v){
                    nc.Turn.v = 1;
                }
            }

            if(Abs(nc.GPlane.v) != 19){
                if(nc.GPolarOrCyl.v == 1){
                    nc.XC_.v = cmd.EP.X * Cos(nc.RotC.v) - cmd.EP.Y * Sin(nc.RotC.v);
                }
                else nc.XC_.v = cmd.EP.X * xScale;
                nc.XC_.Show();
            }

            if(Abs(nc.GPlane.v) != 18){
                if(nc.GPolarOrCyl.v == 1){
                    nc.YC_.v = cmd.EP.Y * Cos(nc.RotC.v) - cmd.EP.X * Sin(nc.RotC.v);
                }
                else nc.YC_.v = cmd.EP.Y;
                nc.YC_.Show();
            }

            if(Abs(nc.GPlane.v) != 17){
                nc.ZC_.Show(cmd.EP.Z);
            }

            nc.Block.Out();

            //current coordinates
            PT_ = new TInp3DPoint(nc.X.v,nc.Y.v,nc.Z.v);
        }

        public override void OnAxesBrake(ICLDAxesBrakeCommand cmd, CLDArray cld)
        {
            foreach(CLDAxisBrake ax in cmd.Axes) 
            {
                if (ax.IsC) 
                {
                    if (ax.StateIsOn)
                    {
                        if(nc.MTorm.v != 10) //&& nc.C.v == MaxReal 
                        {
                            nc.C.v = 0;
                            nc.Block.Out();
                        }

                        nc.MTorm.v = 10;
                    }
                        
                    else nc.MTorm.v = 11;
                }

                else if (ax.IsC2) 
                {
                    if (ax.StateIsOn)
                    {
                        if(nc.MTorm2.v != 10) //&& nc.C.v == MaxReal 
                        {
                            nc.C2.v = 0;
                            nc.Block.Out();
                        }

                        nc.MTorm2.v = 10;
                    }
                        
                    else nc.MTorm2.v = 11;
                }
            }

            nc.Block.Out();
        }

        public override void OnDelay(ICLDDelayCommand cmd, CLDArray cld)
        {
            nc.Block.Out();

            nc.GPause.Show(4);
            nc.FPause.Show(cmd.TimeSpan);

            nc.Block.Out();
        }

        public override void OnCycle(ICLDCycleCommand cmd, CLDArray cld)
        {
            #region Cycle variables
            //int KodCycle;      
            //int CycleNumber;
            double RTP;
            double RFP;
            double SDIS;
            double DP;
            double DPR;
            double DTB;
            double FDEP;
            double FDRP;
            double DAM;
            double DTS;
            double FRF;
            double VARI;
            double VRT;
            double DTD;
            double DIS1;
            double PIT;
            double MDEP;
            #endregion

            if (cld[1] == 72)
            {
                cycle.OffCycle();
                cycle.State.IsFirstCycle = true;
            }

            else
            {
                nc.Block.Out();
                cycle.OnCycle();
                if (cld[4] > 0) // формирование значения подачи
                {
                    nc.F.v = cld[4] == 316 ? cld[4] * nc.S.v : cld[4];
                    if ( nc.F.Changed ) nc.Block.Out();
                }
                
                if (nc.MCoolant.Changed) nc.Block.Out(); // Вывод охлаждения перед циклом

                cycle.State.KodCycle = cld[1];

                #region Setting some cycle variables 

                RTP = cld[8];            // пл-ть отвода (абсол)                 
                RFP = cld[11];           // референтн пл-ть (абсол)
                SDIS = cld[5] - cld[11]; // безопасное растояние (инкр без знака)
                DP = cld[5] - cld[2];    // нижний уровень отверстия (абсол)
                DPR = RFP - DP;             // Глубина отверстия отн реф плоск (без знака)
                DTB = cld[10];           //  Пауза на глубине сверления
                FDEP = RFP - cld[6];  // глубина одного шага сверления abs
                FDRP = cld[6];          // глубина одного шага сверления
                DAM = 0;                   // дегрессия (значение по умолчанию)
                DTS = cld[10] * 2;    // Пауза удаления стружки
                FRF = 1;                   // коэффициент подачи для первой глубины (по умолчанию)
                VARI = 0;                  // переключатель ломка/удаление
                VRT = 2;                   // Отвод при ломке стружки  (по умолчанию)
                DTD = cld[10];          // Пауза на дне
                DIS1 = 1;                  // Недоход при повторных погружениях (по умолчанию)
                PIT = cld[12];          // Шаг резьбы
                
                #endregion

                if (cycle.KodCycle == 153)
                {
                    VARI = 1;
                    if (cycle.IsFirstCycle)
                    {
                        //input
                    }
                }
                
                else if (cycle.KodCycle == 288)
                {
                    VARI = 0;
                    if (cycle.IsFirstCycle)
                    {
                        //input
                    }
                }

                MDEP = Abs(DAM); // Минимальная глубина сверления
                cycle.State.CycleNumber = 0;

                cycle.Prms[1] = RTP;
                cycle.Prms[2] = RFP;
                cycle.Prms[3] = SDIS;
                cycle.Prms[4] = DP;

                switch (cycle.KodCycle)
                {
                    case 163:
                        cycle.State.CycleNumber = 81; //DRILL
                        cycle.Prms[5] = DPR;
                        break;
                    case 81:
                        cycle.State.CycleNumber = 82; // FACE
                        cycle.Prms[6] = DTB;
                        break;
                    case 288:
                        cycle.State.CycleNumber = 83; // BRKCHP
                        cycle.Prms[6] = FDEP;
                        cycle.Prms[8] = DAM;
                        cycle.Prms[9] = DTB;
                        cycle.Prms[11] = FRF;
                        cycle.Prms[12] = VARI;
                        cycle.Prms[14] = MDEP;
                        cycle.Prms[15] = VRT;
                        cycle.Prms[16] = DTD;
                        cycle.Prms[17] = DIS1;
                        break;
                    case 153:
                        cycle.State.CycleNumber = 83; // DEEP
                        cycle.Prms[6] = FDEP;
                        cycle.Prms[8] = DAM;
                        cycle.Prms[9] = DTB;
                        cycle.Prms[10] = DTS;
                        cycle.Prms[11] = FRF;
                        cycle.Prms[12] = VARI;
                        cycle.Prms[14] = MDEP;
                        cycle.Prms[16] = DTD;
                        cycle.Prms[17] = DIS1;
                        break;
                    case 168:
                        cycle.State.CycleNumber = 84; // TAP
                        cycle.Prms[7] = 3;
                        cycle.Prms[9] = PIT;
                        cycle.Prms[11] = nc.S3.v;
                        cycle.Prms[12] = nc.S3.v;
                        cycle.Prms[13] = 3;
                        cycle.Prms[14] = 1;
                        cycle.Prms[15] = 0;
                        cycle.Prms[16] = 0;
                        break;
                }

                if (cycle.CycleNumber > 0)
                {
                    //call OutCycle
                }

                cycle.State.IsFirstCycle = false;
            }
        }

        public override void OnExtCycle(ICLDExtCycleCommand cmd, CLDArray cld)
        {
            #region Cycle variables

            int CDIR;              // Thread direction 2-G2, 3-G3
            double SDIR;           // Spindle rotation direction
            double CurPos;         // Current position (applicate)
            double CPA = 0;            // Absciss
            double CPO = 0;            // Ordinate
            double TempCoord;      // Auxiliary variable
            double RTP, RFP, SDIS, DP, DPR;

            #endregion

            if (cmd.IsOn)
            {
                cycle.OnCycle();
                nc.F.Show(PPFunFeed);
                nc.GInterp.Show(0);
            }

            else if (cmd.IsOff)
            {
                cycle.OffCycle();
            }

            else if (cmd.IsCall)
            {
                cycle.State.CycleNumber = 0;
                cycle.State.CycleName = "CYCLE";
                cycle.State.CycleGeomName = "";

                switch (cmd.CycleType)
                {
                    case 473:
                    case >= 481 and <=491:
                        if (Abs(cld[5]) > Abs(cld[3]) && Abs(cld[5]) > Abs(cld[4])){
                            cycle.State.CyclePlane = -Sgn(cld[5]) * 17; //XY, YX
                        }

                        else if (Abs(cld[4]) > Abs(cld[3]) && Abs(cld[4]) > Abs(cld[5])){
                            cycle.State.CyclePlane = -Sgn(cld[4]) * 18; //ZX, XZ
                        }

                        if (Abs(cld[3]) > Abs(cld[4]) && Abs(cld[3]) > Abs(cld[5])){
                            cycle.State.CyclePlane = -Sgn(cld[3]) * 19; //YZ, Zy
                        }

                        if (Abs(nc.GPlane.v) != Abs(cycle.CyclePlane)){
                            nc.GPlane.v = cycle.CyclePlane;
                            nc.Block.Out();
                        }

                        switch(Abs(nc.GPlane.v))
                        {
                            case 17: // XY
                                CurPos = nc.Z.v;
                                CPA = nc.X.v / xScale;
                                CPO = nc.Y.v;
                                break;
                            case 18: // ZX
                                CurPos = nc.Y.v;
                                CPA = nc.Z.v;
                                CPO = nc.X.v / xScale;
                                break;
                            case 19: // YZ
                                CurPos = nc.X.v / xScale;
                                CPA = nc.Y.v;
                                CPO = nc.Z.v;
                                break;

                            default:
                                CurPos = nc.Z.v;
                                CPA = nc.X.v / xScale;
                                CPO = nc.Y.v;
                                Debug.WriteLine("Undefined cycle plane");
                                break;
                        }

                        if(cycle.CyclePlane < 0){
                            TempCoord = CPA;
                            CPA = CPO;
                            CPO = TempCoord;
                        }

                        // Define base levels
                        RTP = CurPos;
                        RFP = CurPos - cld[7]*Sgn(nc.GPlane.v); // CurPos - Tp
                        SDIS = cld[7] - cld[6]; // Tp - Sf
                        DP = CurPos - cld[8]*Sgn(nc.GPlane.v); // CurPos - Bt
                        DPR = cld[8] - cld[7]; // Bt - Tp
                        // CycleXX(RTP,RFP,SDIS,DP)
                        cycle.Prms[1] = RTP;
                        cycle.Prms[2] = RFP;
                        cycle.Prms[3] = SDIS;
                        cycle.Prms[4] = DP;
                        cycle.State.CycleNumber = 81;

                        switch(cmd.CycleType){
                            case 481: 
                            case 482:
                            case >= 485 and <=489: // Simple drilling
                                cycle.State.CycleNumber = cmd.CycleType - 400;
                                if (cld[15] > 0) cycle.Prms[6] = cld[15]; // Delay in seconds
                                // Spindle rotation direction
                                SDIR = nc.S3.v > 0 ? 3 : 4;

                                if ((cmd.CycleType == 486) || (cmd.CycleType == 488)){
                                    cycle.Prms[7] = SDIR;
                                }  
                                else if (cmd.CycleType == 487) {
                                    cycle.Prms[6] = SDIR;
                                }
                                if (cmd.CycleType == 485) {
                                    cycle.Prms[7] = cld[10]; // WorkFeed
                                    cycle.Prms[8] = cld[14]; // ReturnFeed
                                }

                                else if (cmd.CycleType == 486) {
                                    cycle.Prms[8] = 0; //RPA
                                    cycle.Prms[9] = 0; //RPO
                                    cycle.Prms[10] = 0; // RPAP
                                    cycle.Prms[11] = 0; // POSS
                                }
                                break;
                            case 473:
                            case 483: // Deep drilling (473-chip breaking, 483-chip removing)
                                cycle.State.CycleNumber = 83;
                                cycle.Prms[6] = CurPos - (cld[7]+cld[17])*Sgn(cycle.CyclePlane); // FDEP = CurPos-(Tp+St)
                                cycle.Prms[8] = cld[18]; // DAM - degression
                                cycle.Prms[9] = cld[15]; // DTB - Bottom delay
                                cycle.Prms[10] = cld[16]; // DTS - Top delay
                                cycle.Prms[11] = 1; // FRF - First feed coef
                                cycle.Prms[12] = cmd.CycleType == 473 ? 0 : 1; // VARI - breaking or removing
                                cycle.Prms[14] = cld[18]; // _MDEP - Minimal deep step (=degression)
                                if(cmd.CycleType == 473){
                                    cycle.Prms[15] = cld[20]; // _VRT - LeadOut
                                }
                                else{
                                    cycle.Prms[17] = cld[19]; // _DIS1 - Deceleration
                                }
                                cycle.Prms[16] = 0; //_DTD - finish delay (if 0 then = DTB)
                                break;
                            case 484: // Tapping
                                SDIR = nc.S.v > 0 ? 3 : 4;
                                if (cld[19] == 1) { // Fixed socket
                                    cycle.State.CycleNumber = 84;
                                    cycle.Prms[7] = SDIR; // SDAC
                                    cycle.Prms[9] = nc.S.v > 0 ? cld[17] : -cld[17]; // PIT
                                    cycle.Prms[10] = cld[18]; // POSS
                                    cycle.Prms[14] = 1; // PTAB
                                } 
                                else { // Floating socket
                                    cycle.State.CycleNumber = 840;
                                    cycle.Prms[7] = 0; // SDR
                                    cycle.Prms[8] = SDIR; // SDAC
                                    cycle.Prms[9] = 11; // ENC
                                    cycle.Prms[11] = nc.S.v > 0 ? cld[17] : -cld[17]; // PIT
                                    cycle.Prms[13] = 1;
                                }
                                break;
                            case 490: // Thread milling
                                cycle.State.CycleNumber = 90;
                                cycle.Prms[6] = cld[16]; // DIATH - Outer diameter
                                cycle.Prms[7] = cld[16]; // KDIAM - Inner diameter
                                cycle.Prms[8] = cld[17]; // PIT - thread step
                                cycle.Prms[9] = cld[10]; // FFR - Work feed
                                CDIR = cld[19]; // CDIR - Spiral direction
                                if ((CDIR != 2) && (CDIR != 3)){
                                    if ((nc.S3.v > 0) && (CDIR == 0))       CDIR = 3;
                                    else if ((nc.S3.v <= 0) && (CDIR == 0)) CDIR = 2;
                                    else if ((nc.S3.v > 0) && (CDIR == 1))  CDIR = 2;
                                    else if ((nc.S3.v <= 0) && (CDIR == 1)) CDIR = 3;
                                }
                                cycle.Prms[10] = CDIR;
                                cycle.Prms[11] = cld[18]; // TYPTH - 0-inner, 1-outer thread
                                cycle.Prms[12] = CPA;  // CPA - Center X
                                cycle.Prms[13] = CPO;   // CPO - Center Y
                                break;
                            case 491: // Hole pocketing
                                cycle.State.CycleNumber = 4;
                                cycle.State.CycleName = "POCKET";
                                cycle.Prms[5] = 0.5 * cld[16]; // PRAD - Radius
                                cycle.Prms[6] = CPA; // PA - Center X
                                cycle.Prms[7] = CPO; // PO - Center Y
                                cycle.Prms[8] = cld[20]; // MID - Deep step
                                cycle.Prms[9] = 0; // FAL - finish wall stock
                                cycle.Prms[10] = 0; // FALD - finish deep stock
                                cycle.Prms[11] = cld[10]; // FFP1 - Work feed
                                cycle.Prms[12] = cld[12]; // FFD - Plunge feed
                                CDIR = cld[19];
                                if (CDIR <= 1) CDIR = 1 - CDIR;
                                cycle.Prms[13] = CDIR; // CDIR - Spiral direction
                                cycle.Prms[14] = 21; // VARI - Rough spiral machining
                                cycle.Prms[15] = cld[22]; // MIDA - Horizontal step
                                cycle.Prms[18] = 0.5 * cld[18]; // RAD1 - Spiral radius
                                cycle.Prms[19] = cld[17]; // DP1 - Spiral step
                                break;
                        }

                        break;// 5D Drilling cycles 
                    
                    case >= 400 and <=401: //ЦИКЛ ЧЕРНОВОГО ТОЧЕНИЯ И ЧИСТОВОГО ТОЧЕНИЯ
                        cycle.State.CycleNumber = 95;
                        cycle.State.ContourN += 1;
                        cycle.State.CycleGeomName = "BEG_C" + cycle.ContourN.ToString() + ":END_C" + cycle.ContourN.ToString();
                        cycle.OnCycleGeometry();
                        nc.Block.Hide(nc.GInterp, nc.F);

                        if(cmd.CycleType == 401){
                            cycle.Prms[1] = cld[4]; //MID - шаг чернового прохода
                            cycle.Prms[2] = Abs(cld[7]); // FALZ - чистовой припуск по Z
                            cycle.Prms[3] = Abs(cld[8]); // FALX - чистовой припуск по X
                            cycle.Prms[11] = cld[9]; // VRT - отскок от контура после чернового хода
                        }
                        cycle.Prms[4] = Abs(cld[13]);   // FAL - эквидистантный припуск для контура
                        cycle.Prms[5] = nc.F.v;         // FF1 - подача чернового прохода
                        cycle.Prms[6] = nc.F.v * 0.5;   // FF2 - подача врезания в канавку
                        cycle.Prms[7] = nc.F.v;         // FF3 - подача чистового прохода
                        
                        // Чистовая обработка
                        if(cmd.CycleType == 400) cycle.Prms[8] = 5; 
                        else cycle.Prms[12] = cld[12] == 0 ? 1 : 9; // Без чистового прохода, 1-черновая, 2-комлексная
                        
                        //Без перебега
                        if(cld[6] == 0){
                            cycle.Prms[8] += 200; //С прямым возвратом
                        }
                        if(Abs(cld[11]) == 2) cycle.Prms[8] += 2; //Если внутренняя обработка
                        if(cmd.CycleType == 401 && cld[5] > 0) cycle.Prms[8] += 1; //Если поперечная обработка
                        break;

                    case 402: //ЦИКЛ ТОЧЕНИЯ НАРУЖНЫХ, ВНУТРЕННИХ И ТОРЦЕВЫХ КАНАВОК
                        cycle.State.CycleNumber = 93;
                        cycle.Prms[1] = nc.X.v; // SPD - X начальная
                        cycle.Prms[2] = nc.Z.v; // SPL - Z начальная
                        cycle.Prms[3] = Abs(cld[4]); // WIDG - ширина канавки
                        cycle.Prms[4] = Abs(cld[5]); // DIAG - глубина канавки

                        //Цилиндрическая или торцевая канавка, STA1 - Угол наружный
                        cycle.Prms[5] = cld[3] == 0 ? 0 : 90;
                        cycle.Prms[6] = 0; // ANG1
                        cycle.Prms[7] = 0; // ANG2
                        cycle.Prms[8] = 0; // RCO1
                        cycle.Prms[9] = 0; // RCO2
                        cycle.Prms[10] = 0; // RCI1
                        cycle.Prms[11] = 0; // RCI2
                        cycle.Prms[12] = 0; // FAL1
                        cycle.Prms[13] = 0; // FAL2
                        cycle.Prms[14] = cld[7]; // IDEP - Шаг вдоль глубины канавки
                        cycle.Prms[15] = 1; // DTB - Время выдержки

                        if (cld[3] == 0) // Цилиндрическая канавка
                        {
                            if(cld[4] > 0){
                                cycle.Prms[16] = cld[5] > 0 ? 3 : 1; // слева направо, снизу вверх/сверху вниз
                            }

                            else{
                                cycle.Prms[16] = cld[5] > 0 ? 7 : 5; // справа налево, снизу вверх/сверху вниз
                            }
                        }  

                        else // Торцевая канавка
                        {
                            if(cld[4] > 0){
                                cycle.Prms[16] = cld[5] > 0 ? 4 : 8; // снизу вверх, слева направо/справа налево, 
                            }

                            else{
                                cycle.Prms[16] = cld[5] > 0 ? 2 : 6; // сверху вниз, слева направо/справа налево, 
                            }
                        }  

                        break;

                    case 403: // ЦИКЛ НАРЕЗАНИЯ РЕЗЬБЫ
                        cycle.State.CycleNumber = 97;
                        nc.Block.Hide(nc.GInterp, nc.F);
                        cycle.Prms[1] = cld[23]; // PIT - шаг резьбы
                        cycle.Prms[3] = cld[10]; // SPL - Z начальной точки
                        cycle.Prms[4] = cld[12]; // FPL - Z конечной точки
                        cycle.Prms[5] = (cld[11] + cld[18] ) * 2; // DM1 - X начальной точки
                        cycle.Prms[6] = (cld[13] + cld[18] ) * 2; // DM2 - X конечной точки
                        cycle.Prms[9] = cld[18]; // TDEP - Глубина резьбы
                        cycle.Prms[10] = cld[29]; // FAL - Припуск чистовой
                        cycle.Prms[11] = cld[20]; // IANG - Угол между вертикалью и кромкой витка
                        if (cld[24] == 2) cycle.Prms[11] *= -1; // Попеременно вдоль двух граней
                        cycle.Prms[12] = 0;       // NSP - угол начала первого витка
                        cycle.Prms[13] = cld[28]; // NRC - количество черновых проходов
                        cycle.Prms[14] = cld[30]; // NID - количество "выглаживаний"

                        // VARI - стратегия врезания
                        if (cld[4] == 1) cycle.Prms[15] = cld[25] > 0 ? 2 : 4; // Внутрення резьба
                        else cycle.Prms[15] = cld[25] > 0 ? 1 : 3; // Наружная резьба

                        cycle.Prms[16] = cld[21]; // NUMT - количество заходов (для многозаходной резьбы)
                        cycle.Prms[17] = cld[14]; // VRT - отскок от резьбы по X для перехода в начальную точку
                        break;

                    case 163:
                    case 81: // G81
                        if(cmd.CycleType == 163){
                            cycle.State.CycleNumber = 81;
                        }
                        else{
                            cycle.State.CycleNumber = 82;
                            cycle.Prms[6] = cld[7]; // DTB - выдержка на нижнем уровне
                        }

                        cycle.Prms[1] = cld[6]; // RTP - плоскость отвода
                        cycle.Prms[2] = cld[5]; // RFP - базовая пл-ть (абсол)
                        cycle.Prms[3] = cld[5]-cld[3]; // SDIS - безопасное расстояние
                        cycle.Prms[5] = cld[4]; // DPR - глубина сверления
                        break;
                    
                    case 153:
                    case 288: // ЦИКЛ СВЕРЛЕНИЯ ГЛУБОКИХ ОТВЕРСТИЙ G83
                        cycle.State.CycleNumber = 83;
                        cycle.Prms[1] = cld[6];         // RTP - плоскость отвода
                        cycle.Prms[2] = cld[5];          // RFP - базовая пл-ть (абсол)
                        cycle.Prms[3] = cld[5] - cld[3];  // SDIS - безопасное расстояние
                        cycle.Prms[5] = cld[4];            // DPR - глубина сверления
                        cycle.Prms[6] = cld[3] - cld[11];  // FDEP - глубина первого шага сверления инкр
                        cycle.Prms[8] = cld[14];          // DAM - дегрессия
                        cycle.Prms[9] = cld[7];          // DTB - пауза для ломки стружки
                        cycle.Prms[10] = 0;            // DTS - пауза наверху

                        // VARI - ломка/удаление стружки
                        cycle.Prms[12] = cmd.CycleType == 288 ? 0 : 1;
                        break;

                    case 168: // ЦИКЛ НАРЕЗАНИЯ РЕЗЬБЫ ОСЕВЫМ ИНСТРУМЕНТОМ G84/G184
                        cycle.State.CycleNumber = 84;
                        cycle.Prms[1] = cld[6]; //         RTP - плоскость отвода
                        cycle.Prms[2] = cld[5]; //         RFP - базовая пл-ть (абсол)
                        cycle.Prms[3] = cld[5]-cld[3]; //  SDIS - безопасное расстояние
                        cycle.Prms[5] = cld[4]; //         DPR - глубина сверления

                        // SDAC - направление вращения шпинделя после цикла
                        // SST - обороты шпинделя
                        if (activeLatheSpindle == 1) 
                        {                            
                            cycle.Prms[7] = Abs(nc.MSp.v);
                            cycle.Prms[11] = nc.S.v;
                        }
                        
                        else
                        {
                            cycle.Prms[7] = Abs(nc.MSp2.v);
                            cycle.Prms[11] = nc.S2.v;                              
                        }
                        break;
                }

                if(cycle.CycleNumber > 0){
                    nc.Block.Out();
                    //cycle.OutCycle(CycleName+Str(CycleNumber), CycleGeomName)
                    if(cycle.IsCycleGeometry) // Цикл с геометрией контура
                    {
                        //NCSub.Output(CLD[3]) ! Выводим геометрию
                        cycle.OffCycleGeometry();
                        nc.GInterp.Hide(99999);
                    }
                }

            } //else if CALL end
        }

        public override void OnGoHome(ICLDGoHomeCommand cmd, CLDArray cld)
        {
            if(cycle.CycleOn)
            {
                cycle.OffCycle();
            }

            nc.GInterp.v = 0;
            nc.X.v = FromP_.X;
            nc.Y.v = FromP_.Y;
            nc.Z.v = FromP_.Z;

            if(currentOperationType == OpType.Lathe) nc.Y.Hide();
            nc.Block.Out();
        }

        public override void OnInterpolation(ICLDInterpolationCommand cmd, CLDArray cld)
        {
            base.OnInterpolation(cmd, cld); //904
        }

        public override void OnOpStop(ICLDOpStopCommand cmd, CLDArray cld)
        {
            nc.Block.Out();
            nc.M.v = 1;
            nc.M.v0 = 0;
            nc.Block.Out();
        }

        public override void OnPartNo(ICLDPartNoCommand cmd, CLDArray cld)
        {
            base.OnPartNo(cmd, cld);
            //startproj
        }

        public override void OnPPFun(ICLDPPFunCommand cmd, CLDArray cld)
        {
            base.OnPPFun(cmd, cld);
        }

        public override void OnSinglePassThread(ICLDSinglePassThreadCommand cmd, CLDArray cld)
        {
            nc.GInterp.Show(33);
            nc.F.Show(cmd.ValueAsDouble);
            nc.ThreadStartAngle.v = cmd.StartAngle; //Ориентация шпинделя при многозаходной резьбе
            if(cmd.StartAngle == 0) nc.ThreadStartAngle.v0 = nc.ThreadStartAngle.v;
        }

        public override void OnStop(ICLDStopCommand cmd, CLDArray cld)
        {
            nc.Block.Out();
            nc.M.v = 0;
            nc.M.v0 = 1;
            nc.Block.Out();
        }

        public override void OnFilterString(ref string s, TNCFile ncFile, INCLabel label)
        {
            // if (!NCFiles.OutputDisabled) 
            //     Debug.Write(s);
        }
        
        public override void StopOnCLData() 
        {
            // Do nothing, just to be possible to use CLData breakpoints
        }

    }

}