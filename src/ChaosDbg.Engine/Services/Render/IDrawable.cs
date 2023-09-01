using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ChaosDbg.Render
{
    interface IDrawable
    {
        DrawingGroup DrawingGroup { get; }
    }
}
