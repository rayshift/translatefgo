using System;
using System.Collections.Generic;

namespace RayshiftTranslateFGO.Models
{
    public class ExtraAssetAPIResponse : BaseAPIResponse
    {
        public new Dictionary<string, string> Response { get; set; }
    }

    public class AsyncUploadStartResponse : BaseAPIResponse
    {
        public new Dictionary<string, AsyncUploadStartResponseGuid> Response { get; set; }
    }

    public class AccountLinkTestResponse : BaseAPIResponse
    {
        public new LinkedUserInfo Response { get; set; }
    }

    public class AsyncUploadPieceData
    {
        public Guid guid { get; set; }
        public string data { get; set; }
        public int piece { get; set; }
        public int size { get; set; }
    }


    public class StartUploadPostData
    {
        public int size { get; set; }
        public int pieceCount { get; set; }
    }

    public class AsyncUploadStartResponseGuid
    {
        public Guid guid { get; set; }
    }

    public class LinkedUserInfo
    {
        public string userName { get; set; }
        public bool isPlus { get; set; }
        public UserTokenStatus tokenStatus { get; set; }
    }
    public enum UserTokenStatus
    {
        Missing = 0,
        Active = 1,
        Banned = 2
    }
}