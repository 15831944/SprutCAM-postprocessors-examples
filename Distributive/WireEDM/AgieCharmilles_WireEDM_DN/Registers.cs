// This file may be autogenerated, do not place meaningful code here.
// Use it only to define a list of nc-words (registers) that may appear
// in blocks of the nc-file.
namespace SprutTechnology.SCPostprocessor
{
    ///<summary>A class that defines the nc-file - main output file that should be generated by the postprocessor.</summary>
    public partial class NCFile : TTextNCFile
    {
        ///<summary>The block of the nc-file is an ordered list of nc-words</summary>
        public NCBlock Block;
        public NumericNCWord BlockN = new NumericNCWord("N{0000}", 0);
        public NumericNCWord ProgN = new NumericNCWord("N{0000}", 0);
        ///<summary>Conditions</summary>
        public NumericNCWord C = new NumericNCWord("C{000}", 0);
        ///<summary>G90-G91</summary>
        public NumericNCWord GAbsInc = new NumericNCWord("G{00}", 0);
        ///<summary>G92</summary>
        public NumericNCWord GCS = new NumericNCWord("G{00}", 0);
        ///<summary>G40-G42</summary>
        public NumericNCWord GCompens = new NumericNCWord("G{00}", 0);
        ///<summary>G50-G52</summary>
        public NumericNCWord GTaper = new NumericNCWord("G{00}", 50);
        ///<summary>G60-G61</summary>
        public NumericNCWord G2Contour = new NumericNCWord("G{00}", 60);
        ///<summary>G74-G75</summary>
        public NumericNCWord GUV = new NumericNCWord("G{00}", 75);
        public NumericNCWord GInterp1 = new NumericNCWord("G{00}", 999999);
        ///<summary>Offset</summary>
        public NumericNCWord H = new NumericNCWord("H{000}", 0);
        ///<summary>Offset value</summary>
        public NumericNCWord HValue = new NumericNCWord("= {-####.###}", 0);
        public NumericNCWord X1 = new NumericNCWord("X{-####!###}", 0);
        public NumericNCWord Y1 = new NumericNCWord("Y{-####!###}", 0);
        public NumericNCWord Z1 = new NumericNCWord("Z{-####!###}", 0);
        public NumericNCWord R1 = new NumericNCWord("R{-####!###}", 0);
        public NumericNCWord I1 = new NumericNCWord("I{-####!###}", 0);
        public NumericNCWord J1 = new NumericNCWord("J{-####!###}", 0);
        /// <summary>Colon symbol</summary>
        public NumericNCWord Colon = new NumericNCWord(":{}", 0);
        public NumericNCWord GInterp2 = new NumericNCWord("G{00}", 999999);
        public NumericNCWord X2 = new NumericNCWord("X{-####!###}", 0);
        public NumericNCWord Y2 = new NumericNCWord("Y{-####!###}", 0);
        public NumericNCWord Z2 = new NumericNCWord("Z{-####!###}", 0);
        public NumericNCWord R2 = new NumericNCWord("R{-####!###}", 0);
        public NumericNCWord I2 = new NumericNCWord("I{-####!###}", 0);
        public NumericNCWord J2 = new NumericNCWord("J{-####!###}", 0);
        public NumericNCWord U = new NumericNCWord("U{-####!###}", 0);
        public NumericNCWord V = new NumericNCWord("V{-####!###}", 0);
        public NumericNCWord W = new NumericNCWord("W{-####!###}", 0);
        public NumericNCWord A = new NumericNCWord("A{-####!###}", 0);
        public NumericNCWord RollR1 = new NumericNCWord("R{-####!###}", 0);
        public NumericNCWord RollR2 = new NumericNCWord("R{-####!###}", 0);
        public NumericNCWord MStop = new NumericNCWord("M{00}", 0);
        public NumericNCWord MSub = new NumericNCWord("M{00}", 0);
        public NumericNCWord SubN = new NumericNCWord("P{0000}", 0);
        public NCFile() : base()
        {
            Block = new NCBlock(
                  this,
                  BlockN,
                  ProgN,
                  C,
                  GAbsInc,
                  GCS,
                  GCompens,
                  GTaper,
                  G2Contour,
                  GUV,
                  GInterp1,
                  H,
                  HValue,
                  X1,
                  Y1,
                  Z1,
                  R1,
                  I1,
                  J1,
                  Colon,
                  GInterp2,
                  X2,
                  Y2,
                  Z2,
                  R2,
                  I2,
                  J2,
                  U,
                  V,
                  W,
                  A,
                  RollR1,
                  RollR2,
                  MStop,
                  MSub,
                  SubN);
            OnInit();
        }
    }
}