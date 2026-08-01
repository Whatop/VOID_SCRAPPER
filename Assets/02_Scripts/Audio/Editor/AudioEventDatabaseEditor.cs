#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioEventDatabase))]
public class AudioEventDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("VOID SCRAPPER 편의 기능", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "기존 eventId를 01_ui_click 형식으로 바꾸고 번호순으로 정렬합니다. " +
            "99_music_shop_loop를 만들고 BGM/shop 오디오가 있으면 자동 연결합니다. " +
            "기존 번호 없는 ID도 런타임에서 계속 호환됩니다.",
            MessageType.Info
        );

        AudioEventDatabase database = (AudioEventDatabase)target;

        if (GUILayout.Button("번호 적용 + 정렬 + 99_music_shop_loop 추가"))
        {
            database.EditorApplyNumberingAndEnsureShopMusic();
        }

        if (GUILayout.Button("99_music_shop_loop 항목만 추가"))
        {
            database.EditorEnsureShopMusicEntry();
        }
    }
}
#endif
