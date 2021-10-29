using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using static SprutTechnology.STDefLib.STDef;
using static SprutTechnology.SCPostprocessor.CommonFuncs;
using SprutTechnology.VecMatrLib;
using static SprutTechnology.VecMatrLib.VML;

namespace SprutTechnology.SCPostprocessor
{

    public partial class NCFile: TTextNCFile
    {
        ///<summary>Main nc-programm number</summary>
        public int ProgNumber {get; set;}

        ///<summary>Last point (X, Y, Z) was written to the nc-file</summary>
        public TInp3DPoint LastP {get; set;}

        ///<summary>Current plane third coordinate register Z, Y or X</summary>
        public NumericNCWord PlaneZReg;

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
        NCFile nc;
        ///<summary>Main nc-file (in opposite to subroutine)</summary>
        NCFile mainNC;
        ///<summary>G81-G89 cycle is on</summary>
        bool cycleIsOn = false;
        ///<summary>Current plane sign +1 or -1</summary>
        int planeSign = 1;
        ///<summary>Current plane third coordinate 3, 2 or 1</summary>
        int planeZIndex = 3;
 
        #endregion

        public Postprocessor()
        {
            
        }

        void PrintAllTools(){
            SortedList tools = new SortedList();
            for (int i=0; i<CLDProject.Operations.Count; i++){
                var op = CLDProject.Operations[i];
                if (op.Tool==null || op.Tool.Command==null)
                    continue;
                if (!tools.ContainsKey(op.Tool.Number))
                    tools.Add(op.Tool.Number, op.Tool.Caption);
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
            mainNC = new NCFile();
            nc = mainNC;
            nc.OutputFileName = Settings.Params.Str["OutFiles.NCFileName"];
            // Log.Info("Output file name: " + nc.OutputFileName);
            nc.ProgNumber = Settings.Params.Int["OutFiles.NCProgNumber"];
            if (Settings.Params.Bol["BlockFormat.Numbering"])
                nc.BlockN.Show();
            else
                nc.BlockN.Disable();
            if (Settings.Params.Bol["BlockFormat.Spaces"])
                nc.Block.WordsSeparator = " ";
            else
                nc.Block.WordsSeparator = "";

            nc.WriteLine("%");
            nc.WriteLine("O" + Str(nc.ProgNumber));

            nc.WriteLine();
            nc.WriteLine(String.Format("( {0} )", "Generated by SprutCAM"));
            nc.WriteLine(String.Format("( {0} )", "Date: " + DateTime.Now.ToShortDateString()));
            nc.WriteLine(String.Format("( {0} )", "Time: " + DateTime.Now.ToShortTimeString()));
            nc.WriteLine();

            PrintAllTools();
            nc.WriteLine();

            nc.Block.Show(nc.GAbsInc, nc.GMeasure, nc.GWCS, nc.GLCS, nc.GPlane, 
                nc.GLCompens, nc.GRCompens, nc.GInterp, nc.GCycle);
            nc.Block.Out();
            nc.OutWithN("G53", nc.Z.ToString(0));
            nc.OutWithN("G53", nc.B.ToString(0), nc.C.ToString(0));
        }

        public override void OnFinishProject(ICLDProject prj)
        {
            nc.Block.Out();
            nc.Output("M30");
        }

        public override void OnStartTechOperation(ICLDTechOperation op, ICLDPPFunCommand cmd, CLDArray cld)
        {
            // One empty line between operations if the operation has a new tool 
            if (op.Enabled)
                nc.WriteLine();
            nc.OutWithN("( " + Transliterate(op.CLDFile.Caption) + " )");
            if (op.Tool!=null && op.Tool.Command!=null) {
                nc.OutWithN("G53", nc.Z.ToString(0));
                nc.OutWithN("G53", nc.B.ToString(0), nc.C.ToString(0));
                nc.T.Show(op.Tool.Number);
                nc.M.Show(6);
                nc.TrailingComment.v = Transliterate(op.Tool.Caption);
                nc.TrailingComment.v0 = "";
                nc.Block.Out();
                // var s = nc.Block.Form();
                // nc.WriteLine(s + " ( " + op.Tool.Caption + " )");
                nc.Block.Reset(nc.X, nc.Y, nc.Z, nc.A, nc.B, nc.C);
            }
            if (op.WorkpieceCSCommand!=null) {
                nc.GWCS.v = op.WorkpieceCSCommand.CSNumber;
                nc.Block.Out();
            }
        }

        public override void OnCallNCSub(ICLDSub cldSub, ICLDPPFunCommand cmd, CLDArray cld)
        {
            cldSub.Tag = mainNC.ProgNumber + cldSub.SubCode;
            nc.M.Show(98);
            nc.PSubCall.Show(cldSub.Tag);
            nc.Block.Out();
            if (!cldSub.Translated)
                cldSub.Translate();
        }

        public override void OnStartNCSub(ICLDSub cldSub, ICLDPPFunCommand cmd, CLDArray cld)
        {
            nc = new NCFile();
            nc.ProgNumber = cldSub.Tag;
            string path = Path.GetDirectoryName(mainNC.OutputFileName);
            string name = Path.GetFileNameWithoutExtension(mainNC.OutputFileName);
            string ext = Path.GetExtension(mainNC.OutputFileName);
            nc.OutputFileName = Path.Combine(path, name + "_sub_" + Str(nc.ProgNumber) + ext);
            nc.WriteLine("%");
            nc.WriteLine("O" + Str(nc.ProgNumber));
        }

        public override void OnFinishNCSub(ICLDSub cldSub, ICLDPPFunCommand cmd, CLDArray cld)
        {
            nc.Block.Out();
            nc.M.Show(99);
            nc.Block.Out();
            nc = mainNC;
            nc.Block.Reset(nc.X, nc.Y, nc.Z, nc.A, nc.B, nc.C, nc.F, nc.GInterp);
        }

        public override void OnPlane(ICLDPlaneCommand cmd, CLDArray cld)
        {
            nc.GPlane.v = cmd.PlaneGCode;
            planeSign = cmd.PlaneSign;
            switch (cmd.Plane) {
                case CLDPlaneType.XY: 
                case CLDPlaneType.InvXY:
                    planeZIndex = 3;
                    nc.PlaneZReg = nc.Z;
                    break;
                case CLDPlaneType.ZX: 
                case CLDPlaneType.InvZX:
                    planeZIndex = 2;
                    nc.PlaneZReg = nc.Y;
                    break;
                case CLDPlaneType.YZ: 
                case CLDPlaneType.InvYZ:
                    planeZIndex = 1;
                    nc.PlaneZReg = nc.X;
                    break;
            }
        }

        public override void OnSpindle(ICLDSpindleCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                // Stop if spindle reverse
                if ((cmd.IsClockwiseDir && nc.MSpindle==4) || (!cmd.IsClockwiseDir && nc.MSpindle==3)) {
                    nc.MSpindle.Show(5);
                    nc.Block.Out();
                }
                if (cmd.IsCSS) {
                    nc.GCssRpm.v = 96;
                    nc.S.Show(cmd.CSSValue);
                } else {
                    nc.GCssRpm.v = 97;
                    nc.S.Show(cmd.RPMValue);
                }
                if (cmd.IsClockwiseDir)
                    nc.MSpindle.Show(3);
                else
                    nc.MSpindle.Show(4);
                nc.Block.Out();
            } else if (cmd.IsOff) {
                nc.MSpindle.v = 5;
                nc.Block.Out();
            } else if (cmd.IsOrient) {
                nc.M.Show(19);
                nc.Block.Out();
            }
        }

        public override void OnComment(ICLDCommentCommand cmd, CLDArray cld)
        {
            if (!(cmd.IsOperationName || cmd.IsToolName)) {
                nc.OutWithN("( " + Transliterate(cmd.CLDataS) + " )");
            }
        }

        public override void OnWorkpieceCS(ICLDOriginCommand cmd, CLDArray cld)
        {
            nc.GWCS.v = cmd.CSNumber;
        }

        public override void OnLocalCS(ICLDOriginCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                nc.GLCS.Show(68.2);
                nc.X.Show(cmd.WCS.P.X);
                nc.Y.Show(cmd.WCS.P.Y);
                nc.Z.Show(cmd.WCS.P.Z);
                nc.I.Show(cmd.WCS.N.A);
                nc.J.Show(cmd.WCS.N.B);
                nc.K.Show(cmd.WCS.N.C);
                nc.Block.Out();
                nc.Block.Reset(nc.X, nc.Y, nc.Z);
                nc.OutWithN("G53.1");
            } else {
                nc.GLCS.Show(69);
                nc.Block.Out();
            }
        }

        public override void OnLengthCompensation(ICLDCutComCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                nc.GLCompens.Reset(43);
                nc.HLCompens.Reset(cmd.CorrectorNumber);
            } else {
                nc.GLCompens.Show(49);
                nc.HLCompens.Hide(0);
                nc.Block.Out();
            }
        }

        public override void OnRadiusCompensation(ICLDCutComCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                if (cmd.IsLeftDirection)
                    nc.GRCompens.Show(41);
                else
                    nc.GRCompens.Show(42);
                nc.DRCompens.Show(cmd.CorrectorNumber);
            } else {
                nc.GRCompens.Show(40);
            }
        }

        public override void OnCoolant(ICLDCoolantCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                nc.MCoolant.v = 8;
            } else {
                nc.MCoolant.v = 9;
                nc.Block.Out();
            }
        }

        public override void OnMoveVelocity(ICLDMoveVelocityCommand cmd, CLDArray cld)
        {
            if (cmd.IsRapid) {
                nc.GInterp.v = 0;
            } else {
                if (nc.GInterp == 0)
                    nc.GInterp.v = 1;
                nc.F.v = cmd.FeedValue;
            }
        }

        public override void OnGoto(ICLDGotoCommand cmd, CLDArray cld)
        {
            if (nc.GInterp > 1)
                nc.GInterp.v = 1;
            nc.X.v = cmd.EP.X;
            nc.Y.v = cmd.EP.Y;
            nc.Z.v = cmd.EP.Z;
            if (!cycleIsOn) {
                if (nc.Z.Changed && nc.GLCompens.ValuesDiffer) {
                    nc.Block.Hide(nc.X, nc.Y);
                    nc.Block.Show(nc.GLCompens, nc.HLCompens, nc.Z);
                    nc.Block.Out();
                    nc.Block.UpdateState(nc.X, nc.Y);
                }            
                if (nc.GLCompens == 43.4) {
                    // TCPM
                    nc.Block.Show(nc.X, nc.Y, nc.Z);
                    if (CLDProject.Machine.HasAAxis)
                        nc.A.Show();
                    if (CLDProject.Machine.HasBAxis)
                        nc.B.Show();
                    if (CLDProject.Machine.HasCAxis)
                        nc.C.Show();
                }                
                nc.Block.Out();
            }
            nc.LastP = cmd.EP;
        }

        public override void OnMultiGoto(ICLDMultiGotoCommand cmd, CLDArray cld)
        {
            if (nc.GInterp > 1)
                nc.GInterp.v = 1;
            nc.Block.SetMarks(false);
            foreach(CLDMultiMotionAxis ax in cmd.Axes) {
                if (ax.IsX) {
                    nc.X.v = ax.Value;
                    nc.X.Marked = nc.X.ValuesDiffer;
                } else if (ax.IsY) {
                    nc.Y.v = ax.Value;
                    nc.Y.Marked = nc.Y.ValuesDiffer;
                } else if (ax.IsZ) {
                    nc.Z.v = ax.Value;
                    nc.Z.Marked = nc.Z.ValuesDiffer;
                } else if (ax.IsA) { 
                    nc.A.v = ax.Value;
                    nc.A.Marked = nc.A.ValuesDiffer;
                } else if (ax.IsB)  {
                    nc.B.v = ax.Value;
                    nc.B.Marked = nc.B.ValuesDiffer;
                } else if (ax.IsC) { 
                    nc.C.v = ax.Value;
                    nc.C.Marked = nc.C.ValuesDiffer;
                }
            }
            if (!cycleIsOn) {
                if (nc.Z.Marked && nc.GLCompens.ValuesDiffer) {
                    foreach (NCWord w in nc.Block.MarkedWords) 
                        if (w!=nc.Z)
                            w.Hide();
                    nc.Block.Show(nc.GLCompens, nc.HLCompens, nc.Z);
                    nc.Block.Out();
                    foreach (NCWord w in nc.Block.MarkedWords) 
                        if (w!=nc.Z)
                            w.Show();
                }            
                if (nc.GLCompens == 43.4) {
                    // TCPM
                    nc.Block.Show(nc.X, nc.Y, nc.Z);
                    if (CLDProject.Machine.HasAAxis)
                        nc.A.Show();
                    if (CLDProject.Machine.HasBAxis)
                        nc.B.Show();
                    if (CLDProject.Machine.HasCAxis)
                        nc.C.Show();
                }                
                nc.Block.Out();
            }
            nc.LastP = cmd.EP;
        }

        public override void OnPhysicGoto(ICLDPhysicGotoCommand cmd, CLDArray cld)
        {
            foreach(CLDMultiMotionAxis ax in cmd.Axes) {
                if (ax.IsX) {
                    nc.X.Show(ax.Value);
                } else if (ax.IsY) {
                    nc.Y.Show(ax.Value);
                } else if (ax.IsZ) {
                    nc.Z.Show(ax.Value);
                } else if (ax.IsA) { 
                    nc.A.Show(ax.Value);
                } else if (ax.IsB)  {
                    nc.B.Show(ax.Value);
                } else if (ax.IsC) { 
                    nc.C.Show(ax.Value);
                }
            }
            if (nc.X.Changed || nc.Y.Changed || nc.Z.Changed || nc.A.Changed || nc.B.Changed || nc.C.Changed) {
                nc.GHome.Show(53);
                nc.Block.Out();
            }
        }

        public override void OnGoHome(ICLDGoHomeCommand cmd, CLDArray cld)
        {
            foreach(CLDMultiMotionAxis ax in cmd.Axes) {
                if (ax.IsX) {
                    nc.U.Show(0);
                } else if (ax.IsY) {
                    nc.V.Show(0);
                } else if (ax.IsZ) {
                    nc.W.Show(0);
                }
            }
            if (nc.U.Changed || nc.V.Changed || nc.W.Changed) {
                nc.GHome.Show(28);
                nc.Block.Out();
            }
        }

        public override void OnCircle(ICLDCircleCommand cmd, CLDArray cld)
        {
            nc.GInterp.v = cmd.Dir;
            nc.X.v = cmd.EP.X;
            nc.Y.v = cmd.EP.Y;
            nc.Z.v = cmd.EP.Z;
            nc.R.Show(cmd.RIso);
            switch (Abs(cmd.Plane)) {
                case 17:
                    nc.Block.Show(nc.X, nc.Y);
                    break;
                case 18:
                    nc.Block.Show(nc.Z, nc.X);
                    break;
                case 19:
                    nc.Block.Show(nc.Y, nc.Z);
                    break;
            }
            nc.Block.Out();
        }

        public override void OnAxesBrake(ICLDAxesBrakeCommand cmd, CLDArray cld)
        {
            foreach(CLDAxisBrake ax in cmd.Axes) {
                if (ax.IsA) {
                    if (ax.StateIsOn)
                        nc.MABrake.v = 593;
                    else
                        nc.MABrake.v = 592;
                } else if (ax.IsB) {
                    if (ax.StateIsOn)
                        nc.MBBrake.v = 595;
                    else
                        nc.MBBrake.v = 594;
                } else if (ax.IsC) {
                    if (ax.StateIsOn)
                        nc.MCBrake.v = 597;
                    else
                        nc.MCBrake.v = 596;
                }
            }
            if (nc.MABrake.Changed || nc.MBBrake.Changed || nc.MCBrake.Changed)
                nc.Block.Out();
        }

        public override void OnInterp5x(ICLDInterpolationCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                nc.GLCompens.v = 43.4;
            } else {
                nc.GLCompens.v = 49;
            }
            nc.Block.Out();
        }

        public override void OnHoleExtCycle(ICLDExtCycleCommand cmd, CLDArray cld)
        {
            if (cmd.IsOn) {
                cycleIsOn = true;
                nc.GCycle.Reset(80);
            } else if (cmd.IsOff) {
                nc.GCycle.v = 80;
                nc.Block.Out();
                cycleIsOn = false;
            } else if (cmd.IsCall) {
                int sg = -cld[2+planeZIndex];
                double curPos = nc.LastP[planeZIndex];
                nc.PlaneZReg.v = curPos - cld[8]*sg;
                nc.RSafeLevel.v = curPos - cld[6]*sg;
                if (cld[9] == 0) 
                    nc.F.v = cld[10]*nc.S; 
                else 
                    nc.F.v = cld[10];
                nc.GInterp.Hide(1);
                nc.GCycle.v = cmd.CycleType-400;
                switch (cmd.CycleType) {
                    case CLDConst.W5DDrill:
                        if (nc.GCycle.Changed) 
                            nc.Block.Show(nc.PlaneZReg, nc.RSafeLevel, nc.F);
                        nc.Block.Out();
                        break;
                    case CLDConst.W5DFace:
                        nc.PDrillPause.v = cld[15]*1000;
                        if (nc.GCycle.Changed) 
                            nc.Block.Show(nc.PlaneZReg, nc.RSafeLevel, nc.F, nc.PDrillPause);
                        nc.Block.Out();
                        break;
                    case CLDConst.W5DChipRemoving:
                    case CLDConst.W5DChipBreaking:
                        nc.QStep.v = cld[17];
                        if (nc.GCycle.Changed) 
                            nc.Block.Show(nc.PlaneZReg, nc.RSafeLevel, nc.F, nc.QStep);
                        nc.Block.Out();
                        break;
                    case CLDConst.W5DTap:
                        break;
                    case CLDConst.W5DBore5:
                        break;
                    case CLDConst.W5DBore6:
                        break;
                    case CLDConst.W5DBore7:
                        break;
                    case CLDConst.W5DBore8:
                        break;
                    case CLDConst.W5DBore9:
                        break;
                    case CLDConst.W5DThreadMill:
                        break;
                    case CLDConst.W5DHolePocketing:
                        break;
                    case CLDConst.W5DGrooveBoring:
                        break;
                }
            }
        }

        public override void OnStop(ICLDStopCommand cmd, CLDArray cld)
        {
            nc.Block.Out();
            nc.M.Show(0);
            nc.Block.Out();
        }

        public override void OnOpStop(ICLDOpStopCommand cmd, CLDArray cld)
        {
            nc.Block.Out();
            nc.M.Show(1);
            nc.Block.Out();
        }

        public override void OnDelay(ICLDDelayCommand cmd, CLDArray cld)
        {
            nc.GDelay.Show(4);
            nc.XDelay.Show(cmd.TimeSpan);
            nc.Block.Out();
        }

        public override void OnBeforeCommandHandle(ICLDCommand cmd, CLDArray cld)
        {
            
        }

        
        public override void StopOnCLData() 
        {
            // Do nothing, just to be possible to use CLData breakpoints
        }

    }
}