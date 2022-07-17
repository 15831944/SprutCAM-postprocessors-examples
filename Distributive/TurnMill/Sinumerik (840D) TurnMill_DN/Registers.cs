// This file may be autogenerated, do not place meaningful code here. 
// Use it only to define a list of nc-words (registers) that may appear 
// in blocks of the nc-file.
namespace SprutTechnology.SCPostprocessor
{
    ///<summary>A class that defines the nc-file - main output file that should be generated by the postprocessor.</summary>
    public partial class NCFile: TTextNCFile
    {
        ///<summary>The block of the nc-file is an ordered list of nc-words</summary>
        public NCBlock Block;

        ///<summary>Automatic block numbers counter</summary>
        public CountingNCWord BlockN = new CountingNCWord("N{######}", 1, 1, 1);

        ///<summary>G90, G91 - absolute or incremental motion mode selection</summary>
        public NumericNCWord GAbsInc = new NumericNCWord("G{##}", 90);

        ///<summary>G20, G21 - current measurements inch or mm</summary>
        public NumericNCWord GMeasure = new NumericNCWord("G{##}", 21);

        ///<summary>G54-G59 - current workpiece coordinate system number</summary>
        public NumericNCWord GWCS = new NumericNCWord("G{##}", 54);

        ///<summary>G04 - delay of nc programm execution</summary>
        public NumericNCWord GDelay = new NumericNCWord("G{00}", 04);

        ///<summary>Pause duration in seconds</summary>
        public NumericNCWord XDelay = new NumericNCWord("X{-#####!###}", 0);

        ///<summary>G17, G18, G19 - current plane code</summary>
        public NumericNCWord GPlane = new NumericNCWord("G{##}", 17);

        ///<summary>G50 - maximal spindle rotations definition code</summary>
        public NumericNCWord GSMax = new NumericNCWord("G{##}", 50);

        ///<summary>G96, G97 - spindle rotation mode: const surface speed or const rotations per min</summary>
        public NumericNCWord GCssRpm = new NumericNCWord("G{##}", 96);

        ///<summary>Spindle speed value</summary>
        public NumericNCWord S = new NumericNCWord("S{####}", 0);

        ///<summary>M03, M04, M05 - spindle switch on-off code</summary>
        public NumericNCWord MSpindle = new NumericNCWord("M{00}", 5);

        ///<summary>G94, G95 - current feed mode: per minute or per rev</summary>
        public NumericNCWord GFeed = new NumericNCWord("G{##}", 17);

        ///<summary>G00, G01, G02, G03 - current mode of an interpolation</summary>
        public NumericNCWord GInterp = new NumericNCWord("G{00}", 0);

        ///<summary>G12.1, G13.1, G07.1 - cylindrical or polar interpolation mode</summary>
        public NumericNCWord GPolarOrCyl = new NumericNCWord("G{00.#}", 0);

        ///<summary>G28 - return to home position, or G53 - physic axes motion</summary>
        public NumericNCWord GHome = new NumericNCWord("G{##}", 28);

        ///<summary>G80, G81, G82... - canned cycle code</summary>
        public NumericNCWord GCycle = new NumericNCWord("G{##}", 80);

        ///<summary>G40, G41, G42 - radius compensation mode: off, left, right</summary>
        public NumericNCWord GRCompens = new NumericNCWord("G{##}", 40);

        ///<summary>X coordinate of the movement</summary>
        public NumericNCWord X = new NumericNCWord("X{-#####!###}", 0);

        ///<summary>Y coordinate of the movement</summary>
        public NumericNCWord Y = new NumericNCWord("Y{-#####!###}", 0);

        ///<summary>Z coordinate of the movement</summary>
        public NumericNCWord Z = new NumericNCWord("Z{-#####!###}", 0);

        ///<summary>C axis angle</summary>
        public NumericNCWord C = new NumericNCWord("C{-#####!###}", 0);

        ///<summary>U - incremental X axis movement</summary>
        public NumericNCWord U = new NumericNCWord("U{-#####!###}", 0);

        ///<summary>V - incremental Y axis movement</summary>
        public NumericNCWord V = new NumericNCWord("V{-#####!###}", 0);

        ///<summary>W - incremental Z axis movement</summary>
        public NumericNCWord W = new NumericNCWord("W{-#####!###}", 0);

        ///<summary>H - incremental C axis movement</summary>
        public NumericNCWord H = new NumericNCWord("H{-#####!###}", 0);

        ///<summary>Circle radius</summary>
        public NumericNCWord R = new NumericNCWord("R{-#####!###}", 0);

        ///<summary>Safe return level R in hole machining cycles</summary>
        public NumericNCWord RSafeLevel = new NumericNCWord("R{-#####!###}", 0);

        ///<summary>X coordinate of the circle center point</summary>
        public NumericNCWord I = new NumericNCWord("I{-#####!###}", 0);

        ///<summary>Y coordinate of the circle center point</summary>
        public NumericNCWord J = new NumericNCWord("J{-#####!###}", 0);

        ///<summary>Z coordinate of the circle center point</summary>
        public NumericNCWord K = new NumericNCWord("K{-#####!###}", 0);

        ///<summary>Feedrate value of the movement</summary>
        public NumericNCWord F = new NumericNCWord("F{#####}", 0);

        ///<summary>Tool number (first two numbers)</summary>
        public NumericNCWord T = new NumericNCWord("T{00}", 0);

        ///<summary>Tool number (second two numbers)</summary>
        public NumericNCWord TCor = new NumericNCWord("{00}", 0);

        ///<summary>Pause onhole cycle bottom or top level (G82 and others)</summary>
        public NumericNCWord PDrillPause = new NumericNCWord("P{#####}", 0);

        ///<summary>P - subroutine number in call instruction M98 P####</summary>
        public NumericNCWord PSubCall = new NumericNCWord("P{#####}", 0);

        ///<summary>Q - thread start angle for G32/G33</summary>
        public NumericNCWord QThreadAngle = new NumericNCWord("Q{####!###}", 0);

        ///<summary>Q - step for G73, G83 and others drill cycles</summary>
        public NumericNCWord QStep = new NumericNCWord("Q{#####.###}", 0);

        ///<summary>M00, M01, M02, M30 - auxiliary M codes</summary>
        public NumericNCWord M = new NumericNCWord("M{00}", 0);

        ///<summary>M08, M09 - coolant switch on-off codes</summary>
        public NumericNCWord MCoolant = new NumericNCWord("M{00}", 09);

        ///<summary>M597, M596 - C axis brake switch on-off codes</summary>
        public NumericNCWord MCBrake = new NumericNCWord("M{###}", 596);

        ///<summary>Text comment at the end of the block</summary>
        public TextNCWord TrailingComment = new TextNCWord("( ", "", " )");

        public NCFile(): base()
        {
            Block = new NCBlock(this, 
                BlockN,
                GAbsInc,
                GMeasure,
                GWCS,
                GDelay,
                GPlane,
                GSMax,
                GCssRpm,
                GFeed,
                GInterp,
                GPolarOrCyl,
                GHome,
                GCycle,
                GRCompens,
                X,
                Y,
                Z,
                C,
                U,
                V,
                W,
                H,
                R,
                RSafeLevel,
                I,
                J,
                K,
                XDelay,
                F,
                PDrillPause,
                QStep,
                QThreadAngle,
                S,
                T,
                TCor,
                M,
                MSpindle,
                MCoolant,
                MCBrake,
                PSubCall,
                TrailingComment
            );
            OnInit();
        }

    }
}