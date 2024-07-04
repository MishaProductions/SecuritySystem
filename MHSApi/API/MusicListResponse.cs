using SecuritySystemApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class MusicListResponse
    {
        public List<MusicListEntity> Annoucements { get; set; } = new List<MusicListEntity>();
        public List<MusicListEntity> Music { get; set; } = new List<MusicListEntity>();
    }

    public class MusicListEntity
    {
        public string FileName { get; set; } = "";

        public MusicListEntity(string fileName)
        {
            FileName = fileName;
        }

        public MusicListEntity() { }
    }
}
