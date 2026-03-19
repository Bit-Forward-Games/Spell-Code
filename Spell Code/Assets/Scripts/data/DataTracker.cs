using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

public static class DataSaver
{
    public static SaveData MakeSaver(bool isRemoteAvailable)
    {
        if (isRemoteAvailable)
        {
            return new SaveDataRemote();
        }
        return new SaveDataLFS();
    }
}

//abstract data saving class
public abstract class SaveData
{
    //save data item encapsulating player
    //asyc via coroutines so that gameplay isn't slowed
    public abstract IEnumerator Save(SaveDataHolder data);
}

//data saving class for local file system
public class SaveDataLFS : SaveData
{
    //file path and max files
    string filePath;
    int fileLimit;

    //get the file path
    public SaveDataLFS()
    {
        filePath = Application.dataPath + "/PlayerData/";
        fileLimit = 10;
    }

    //save all data to directory
    public override IEnumerator Save(SaveDataHolder data)
    {
        Debug.Log("Began Data Save");
        //where data will be locally written
        string fileName = Guid.NewGuid().ToString() + ".json";
        string path = Path.GetDirectoryName(filePath + fileName);

        //if the path doesnt exist, make it
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string output = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });

        //write the data to the directory
        File.WriteAllText(filePath + fileName, output);
        Debug.Log("Data Save Complete to: " + filePath);

        yield return null;
    }

    //get a list of all of our stored json files
    public List<FileInfo> GetDataFiles()
    {
        string path = Path.GetDirectoryName(filePath);

        //if the path doesnt exist, make it
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        List<FileInfo> files = new DirectoryInfo(filePath).GetFiles(".json").OrderByDescending(f => f.LastWriteTime).ToList();
        return files;
    }

    //read contents of a file
    public string GetFileContent(string filePath)
    {
        return File.ReadAllText(filePath);
    }
}

//save data to remote
public class SaveDataRemote : SaveData
{
    public override IEnumerator Save(SaveDataHolder data)
    {
        yield break;
    }

    //sync files to remote
    //file = index of file to sync
    //data = string of json-compliant data
    //callback = func to call
    public IEnumerator Sync(int file, string data, Action<int, bool> callback)
    {
        yield break;
    }
}