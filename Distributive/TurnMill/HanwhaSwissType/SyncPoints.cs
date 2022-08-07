using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using static SprutTechnology.STDefLib.STDef;
using SprutTechnology.VecMatrLib;
using static SprutTechnology.VecMatrLib.VML;

namespace SprutTechnology.SCPostprocessor
{


    public class TSyncPoints
    {
        int fLastPoint = 500;
        Dictionary<int, int> fList;
        bool fSynchronized;
        Postprocessor fPost;
        int step = 1;

        public TSyncPoints(Postprocessor post)
        {
            fList = new Dictionary<int, int>();
            fPost = post;
        }

        public void Add(int scSyncPoint = -1) 
        {
            if (!fSynchronized || (scSyncPoint>=0)) {
                if (scSyncPoint>=0) {
                    int pnt;
                    if (!fList.TryGetValue(scSyncPoint, out pnt)) {
                        pnt = fLastPoint + step;
                        fList.Add(scSyncPoint, pnt);
                    }
                    fPost.nc.WriteLine("M"+pnt);
                    fLastPoint = pnt;
                } else {
                    fLastPoint = fLastPoint + step;
                    fPost.nc1.WriteLine("M"+fLastPoint);
                    fPost.nc2.WriteLine("M"+fLastPoint);
                }
                if (fLastPoint>599) {
                    Log.Error("Exceeded the maximum number of wait marks.");
                }
            }
            fSynchronized = true;
        }
        
        public void OutPrev(int scSyncPoint)
        {
            int pnt;
            if (fList.TryGetValue(scSyncPoint, out pnt)) {
                pnt-= step;
            }else{
                fLastPoint += step;
                pnt=fLastPoint;
            }
            fPost.nc.WriteLine("M"+pnt);
        }

        public void CheckForOutput(string s) 
        {
            if (!fSynchronized || String.IsNullOrEmpty(s) || s.StartsWith("("))   
                return;
            fSynchronized = false;
        }

        public void ResetSynchronized()
        {
            fSynchronized = false;
        }
    }

}