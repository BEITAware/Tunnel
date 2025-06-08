using System;
using System.Windows;

namespace Tunnel_Next.Services
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName = "Aero")
        {
            // 清除当前主题资源
            Application.Current.Resources.MergedDictionaries.Clear();
            
            // 添加主题资源字典
            ResourceDictionary themesDict = new ResourceDictionary 
            { 
                Source = new Uri($"pack://application:,,,/Resources/ThemesResourceDictionary.xaml") 
            };
            
            Application.Current.Resources.MergedDictionaries.Add(themesDict);
        }
    }
} 