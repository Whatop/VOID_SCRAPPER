using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string fileName = "void_scrapper_save.json";
    [SerializeField] private bool logSavePath = true;

    public SaveData CurrentSaveData { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, fileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SaveManager가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public SaveData LoadOrCreate()
    {
        if (logSavePath)
        {
            Debug.Log($"Save Path: {SavePath}");
        }

        if (!File.Exists(SavePath))
        {
            CurrentSaveData = CreateDefaultSave();
            Save(CurrentSaveData);
            return CurrentSaveData;
        }

        string json = File.ReadAllText(SavePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            CurrentSaveData = CreateDefaultSave();
            Save(CurrentSaveData);
            return CurrentSaveData;
        }

        CurrentSaveData = JsonUtility.FromJson<SaveData>(json);

        if (CurrentSaveData == null)
        {
            CurrentSaveData = CreateDefaultSave();
            Save(CurrentSaveData);
        }

        return CurrentSaveData;
    }

    public void Save(PermanentProgress progress)
    {
        if (progress == null)
        {
            Debug.LogWarning("저장할 PermanentProgress가 없습니다.", this);
            return;
        }

        Save(progress.CreateSaveData());
    }

    public void Save(SaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("저장할 SaveData가 없습니다.", this);
            return;
        }

        CurrentSaveData = saveData;

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        CurrentSaveData = CreateDefaultSave();
        Save(CurrentSaveData);
    }

    private SaveData CreateDefaultSave()
    {
        SaveData saveData = new SaveData();

        saveData.buildingLevels.Add(new BuildingSaveData(BuildingType.Hangar, 0));
        saveData.buildingLevels.Add(new BuildingSaveData(BuildingType.EngineWorkshop, 0));
        saveData.buildingLevels.Add(new BuildingSaveData(BuildingType.WeaponLab, 0));
        saveData.buildingLevels.Add(new BuildingSaveData(BuildingType.RecoveryProcessor, 0));

        return saveData;
    }
}