using System;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Systems;
using Android.Util;
using IO.Rayshift.Translatefgo;
using Java.Interop;
using Xamarin.Forms;

namespace RayshiftTranslateFGO.Droid
{
    public class NextGenFSServiceConnection: Java.Lang.Object, IServiceConnection, INGFSService
    {
        static readonly JniPeerMembers _members = new XAPeerMembers("io/rayshift/translatefgo$Default", typeof(NGFSServiceDefault));
        public INGFSService Binder { get; private set; }

        private static readonly string BinderError =
            "Shizuku binder dead. Make sure Shizuku is running, and restart the app.";

        public NextGenFSServiceConnection()
        {
            Binder = null;
        }

        private static int CHUNK_SIZE = 1024*400;

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            if (service != null && service.PingBinder())
            {
                Binder = NGFSServiceStub.AsInterface(service);
                Log.Info("TranslateFGO", "NGFS bound; pid=" + Os.Getpid() + ", uid=" + Os.Getuid());
                MessagingCenter.Send(Xamarin.Forms.Application.Current, "shizuku_bound");
            }
        }
        public void OnServiceDisconnected(ComponentName name)
        {
            Log.Warn("TranslateFGO", "NGFS unbound");
            MessagingCenter.Send(Xamarin.Forms.Application.Current, "shizuku_unbound");
            Binder = null;
        }

        public void Destroy()
        {
            Binder?.Destroy();
        }

        public void Exit()
        {
            Binder?.Exit();
        }

        public bool CopyFile(string source, string destination, NGFSError error)
        {
            var result = Binder?.CopyFile(source, destination, error);
            if (result != null) return (bool)result;

            error.IsSuccess = false;
            error.Error = BinderError;
            return false;
        }

        public int GetExistingFileSize(string filename, NGFSError error)
        {
            var result = Binder?.GetExistingFileSize(filename, error);
            if (result != null) return (int)result;

            error.IsSuccess = false;
            error.Error = BinderError;
            return -1;
        }

        public bool GetFileExists(string filename, NGFSError error)
        {
            var result = Binder?.GetFileExists(filename, error);
            if (result != null) return (bool)result;

            error.IsSuccess = false;
            error.Error = BinderError;
            return false;
        }

        public long GetFileModTime(string filename, NGFSError error)
        {
            var result = Binder?.GetFileModTime(filename, error);

            if (result != null) return (long)result;

            error.IsSuccess = false;
            error.Error = BinderError;

            return -1;
        }

        public string[] ListDirectoryContents(string filename, NGFSError error)
        {
            var result = Binder?.ListDirectoryContents(filename, error);

            if (result != null) return result;

            error.IsSuccess = false;
            error.Error = BinderError;

            return null;
        }

        public byte[] ReadExistingFile(string filename, int offset, int length, NGFSError error)
        {
            var result = Binder?.ReadExistingFile(filename, offset, length, error);

            if (result != null) return result;

            error.IsSuccess = false;
            error.Error = BinderError;

            return null;
        }

        public byte[] ReadExistingFile(string filename, NGFSError error) // this is completely **ing dumb that buffer is 1MB, maybe could pass in temporary file, to try
        {
            // find length
            var size = GetExistingFileSize(filename, error);

            if (size == -1 || !error.IsSuccess) return null;

            Log.Info("TranslateFGO", $"Reading {filename} size {size} in chunks");

            int currentOffset = 0;
            int finalLength = size;

            byte[] buffer = new byte[size];

            while (currentOffset < finalLength)
            {
                var thisRead = Math.Min(finalLength - currentOffset, CHUNK_SIZE);
                var readBytes = ReadExistingFile(filename, currentOffset, thisRead, error);

                if (readBytes == null || !error.IsSuccess)
                {
                    return null;
                }
                
                Array.Copy(readBytes, 0, buffer, currentOffset, thisRead);
                currentOffset += thisRead;
            }

            return buffer;
        }

        public bool RemoveFileIfExists(string filename, NGFSError error)
        {
            var result = Binder?.RemoveFileIfExists(filename, error);
            if (result != null) return (bool)result;

            error.IsSuccess = false;
            error.Error = BinderError;

            return false;
        }

        public bool WriteFileContents(string filename, byte[] contents, int offset, int length, NGFSError error)
        {
            var result = Binder?.WriteFileContents(filename, contents, offset, length, error);
            if (result != null) return (bool)result;

            error.IsSuccess = false;
            error.Error = BinderError;

            return false;
        }

        public bool WriteFileContents(string filename, byte[] contents, NGFSError error)
        {
            int currentOffset = 0;
            int finalLength = contents.Length;

            byte[] buffer = new byte[CHUNK_SIZE];

            Log.Info("TranslateFGO", $"Writing {filename} size {finalLength} in chunks");

            while (currentOffset < finalLength)
            {
                buffer.Initialize();
                var thisWrite = Math.Min(finalLength - currentOffset, CHUNK_SIZE);
                Array.Copy(contents, currentOffset, buffer, 0, thisWrite);

                var result = WriteFileContents(filename, buffer, currentOffset, thisWrite, error);

                if (!result || !error.IsSuccess)
                {
                    return false;
                }
                currentOffset += thisWrite;
            }

            return true;
        }

        [Register("asBinder", "()Landroid/os/IBinder;", "GetAsBinderHandler")]
        public virtual unsafe global::Android.OS.IBinder AsBinder()
        {
            try
            {
                JniObjectReference val = _members.InstanceMethods.InvokeVirtualObjectMethod("asBinder.()Landroid/os/IBinder;", this, null);
                return Java.Lang.Object.GetObject<IBinder>(val.Handle, JniHandleOwnership.TransferLocalRef);
            }
            finally
            {
            }
        }
    }
}