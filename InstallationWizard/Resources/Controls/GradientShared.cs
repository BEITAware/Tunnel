using System.ComponentModel;

namespace InstallationWizard.Resources.Controls
{
    public enum GradientType
    {
        Linear,
        Radial
    }

    public interface IGradientStopData : INotifyPropertyChanged
    {
        double Offset { get; set; }
        double H { get; set; }
        double S { get; set; }
        double L { get; set; }
    }
} 