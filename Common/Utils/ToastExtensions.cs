using BlazorBootstrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utils
{
    public static class ToastExtensions
    {
        public static void ShowSuccess(this ToastService toastService, string mensagem)
        {
            toastService.Notify(new(ToastType.Success, mensagem));
        }

        public static void ShowError(this ToastService toastService, string mensagem)
        {
            toastService.Notify(new(ToastType.Danger, mensagem));
        }

        public static void ShowWarning(this ToastService toastService, string mensagem)
        {
            toastService.Notify(new(ToastType.Warning, mensagem));
        }

        public static void ShowInfo(this ToastService toastService, string mensagem)
        {
            toastService.Notify(new(ToastType.Info, mensagem));
        }
    }
}
