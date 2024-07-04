using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages
{
    public class SecurityPage : UserControl
    {
        public event EventHandler? OnShowLoadingBar;
        public event EventHandler? OnHideLoadingBar;
        public event LoadingProgressHandler? OnLoadProgress;
        public virtual void OnNavigateTo()
        {

        }

        public virtual void OnNavigateAway()
        {

        }

        public virtual void OnZoneUpdate()
        {

        }

        public virtual void OnSysTimer(bool arming, int timer)
        {

        }

        public virtual void OnConnected()
        {

        }

        public virtual void OnSystemDisarm()
        {

        }

        public virtual void OnMusicVolChanged()
        {

        }
        public virtual void OnAnncVolChanged()
        {

        }

        public virtual void HideLoadingBar()
        {
            OnHideLoadingBar?.Invoke(this, EventArgs.Empty);
        }
        public virtual void ShowLoadingBar()
        {
            OnShowLoadingBar?.Invoke(this, EventArgs.Empty);
        }
        public virtual void UpdateLoadingString(string progress)
        {
            OnLoadProgress?.Invoke(progress);
        }

        public delegate void LoadingProgressHandler(string progress);
    }
}
