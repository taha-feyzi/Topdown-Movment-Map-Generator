using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TopDownWorldGen.Editor
{
    /// <summary>Tools > Top-Down World Generator > World Generator</summary>
    public sealed class WorldGeneratorWindow : EditorWindow
    {
        private WorldGenerator _gen;
        private WorldGenerationSettings _settings;
        private WorldSaveData _loadData;
        private Vector2 _scroll;
        private bool _alsoDependents = true;
        private bool _useColliders;
        private SerializedObject _so;

        private readonly bool[] _fold = { true, true, false, false, false, false, false, false };

        [MenuItem("Tools/Top-Down World Generator/World Generator")]
        public static void Open()
        {
            var w = GetWindow<WorldGeneratorWindow>("World Generator");
            w.minSize = new Vector2(380, 500);
        }

        private void OnEnable() { AutoFind(); }
        private void OnFocus() { if (_gen == null) AutoFind(); }

        private void AutoFind()
        {
            if (_gen == null) _gen = Object.FindFirstObjectByType<WorldGenerator>();
            if (_gen != null && _settings == null) _settings = _gen.settings;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);
            DrawSetup();
            if (_gen != null && _settings != null)
            {
                if (_so == null || _so.targetObject != _settings) _so = new SerializedObject(_settings);
                _so.Update();
                DrawSettings();
                _so.ApplyModifiedProperties();
                DrawControls();
                DrawPreview();
                DrawSaveLoad();
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- setup
        private void DrawSetup()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _gen = (WorldGenerator)EditorGUILayout.ObjectField("World Generator", _gen, typeof(WorldGenerator), true);
            _settings = (WorldGenerationSettings)EditorGUILayout.ObjectField("Settings", _settings, typeof(WorldGenerationSettings), false);
            if (EditorGUI.EndChangeCheck() && _gen != null && _settings != null && _gen.settings != _settings)
            {
                Undo.RecordObject(_gen, "Assign settings");
                _gen.settings = _settings;
                EditorUtility.SetDirty(_gen);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_gen == null && GUILayout.Button("Create Generator In Scene")) CreateGenerator();
                if (_settings == null && GUILayout.Button("Create Settings Asset")) CreateSettings();
                if (GUILayout.Button("Create Demo Assets")) { DemoAssetCreator.CreateDemoAssets(); AutoFind(); }
            }
            if (_gen != null && _settings != null && _gen.settings != _settings) _gen.settings = _settings;
            if (_gen != null && _settings == null && _gen.settings != null) _settings = _gen.settings;
            if (_gen == null || _settings == null)
                EditorGUILayout.HelpBox("1) Create Generator In Scene  2) Create Demo Assets (or assign your own settings + BiomeData)  3) Generate World.", MessageType.Info);
            EditorGUILayout.Space(6);
        }

        private void CreateGenerator()
        {
            var go = new GameObject("World Generator");
            _gen = go.AddComponent<WorldGenerator>();
            _gen.tilemaps = go.GetComponent<WorldTilemaps>();
            _gen.tilemaps.EnsureLayers();
            if (_settings != null) _gen.settings = _settings;
            Undo.RegisterCreatedObjectUndo(go, "Create World Generator");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private void CreateSettings()
        {
            string dir = "Assets/TopDownWorldGenerator/Settings";
            System.IO.Directory.CreateDirectory(dir);
            var s = CreateInstance<WorldGenerationSettings>();
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/WorldSettings.asset");
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            _settings = s;
            if (_gen != null) _gen.settings = s;
        }

        // ---------------------------------------------------------------- settings sections
        private void DrawSettings()
        {
            if (Section(0, "WORLD SETTINGS")) { P("width"); P("height"); P("seed"); P("randomSeed"); P("generationMode"); P("chunkSize"); P("chunksPerFrame"); P("objectsPerFrame"); }
            if (Section(1, "BIOME SETTINGS")) { P("biomes"); P("biomeScale"); P("biomeWarp"); P("transitionWidth"); P("transitionBlend"); }
            if (Section(2, "TERRAIN SETTINGS")) { P("groundDensity"); P("noiseScale"); P("noiseOctaves"); P("persistence"); P("lacunarity"); P("caIterations"); P("terrainVariation"); P("dirtScale"); P("dirtDensity"); }
            if (Section(3, "VEGETATION SETTINGS")) { P("treeDensity"); P("bushDensity"); P("rockDensity"); P("decorationDensity"); P("objectSpacing"); P("maxObjects"); }
            if (Section(4, "PATH SETTINGS")) { P("pathWidth"); P("branchCount"); P("branchLength"); P("pathCurviness"); P("waypointCount"); P("pathSmoothing"); P("pathWaterCost"); P("startPosition"); P("exitPositions"); }
            if (Section(5, "DARKNESS SETTINGS")) { P("darknessMultiplier"); P("darknessVariation"); P("darknessScale"); P("lightDensity"); P("lightMinDistance"); P("safeZoneRadius"); P("pathLightSpacing"); P("corruptionDensity"); P("corruptionScale"); }
            if (Section(6, "POI SETTINGS")) { P("pois"); P("poiDensity"); }
        }

        private bool Section(int i, string title)
        {
            _fold[i] = EditorGUILayout.BeginFoldoutHeaderGroup(_fold[i], title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return _fold[i];
        }

        private void P(string name)
        {
            var p = _so.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, true);
            else EditorGUILayout.HelpBox("Missing property: " + name, MessageType.Warning);
        }

        // ---------------------------------------------------------------- controls
        private void DrawControls()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("GENERATION CONTROLS", EditorStyles.boldLabel);
            _alsoDependents = EditorGUILayout.ToggleLeft("Also rebuild dependent layers (recommended)", _alsoDependents);

            if (GUILayout.Button("Generate World", GUILayout.Height(30)))
            {
                if (!_gen.HasGeneratedContent() || EditorUtility.DisplayDialog("Generate World",
                    "This replaces all generated tiles and objects (manual edits to them are lost). Undo is available.", "Generate", "Cancel"))
                    Run(GenerationStage.All, "Generate World");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Terrain"))
                    Run(_alsoDependents ? GenerationStage.All : GenerationStage.Terrain, "Regenerate Terrain");
                if (GUILayout.Button("Regenerate Vegetation"))
                    Run(_alsoDependents ? (GenerationStage.Vegetation | GenerationStage.Darkness) : GenerationStage.Vegetation, "Regenerate Vegetation");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Paths"))
                    Run(_alsoDependents ? (GenerationStage.Paths | GenerationStage.POI | GenerationStage.Vegetation | GenerationStage.Darkness) : GenerationStage.Paths, "Regenerate Paths");
                if (GUILayout.Button("Regenerate POIs"))
                    Run(_alsoDependents ? (GenerationStage.POI | GenerationStage.Vegetation | GenerationStage.Darkness) : GenerationStage.POI, "Regenerate POIs");
                if (GUILayout.Button("Regenerate Darkness")) Run(GenerationStage.Darkness, "Regenerate Darkness");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Randomize Seed"))
                {
                    Undo.RecordObject(_settings, "Randomize Seed");
                    _settings.seed = WorldGenerator.NewRandomSeed();
                    EditorUtility.SetDirty(_settings);
                }
                if (GUILayout.Button("Clear Generated World"))
                {
                    if (EditorUtility.DisplayDialog("Clear Generated World", "Remove all generated tiles and generated objects?", "Clear", "Cancel"))
                    {
                        Undo.IncrementCurrentGroup();
                        int g = Undo.GetCurrentGroup();
                        _gen.Clear(GenerationStage.All);
                        Undo.CollapseUndoOperations(g);
                        EditorSceneManager.MarkSceneDirty(_gen.gameObject.scene);
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _useColliders = EditorGUILayout.ToggleLeft("Include colliders", _useColliders, GUILayout.Width(130));
                if (GUILayout.Button("Validate Walkability"))
                {
                    var r = _gen.ValidateScene(_useColliders);
                    if (r != null)
                        EditorUtility.DisplayDialog("Walkability", r.ToString() + (r.Success ? "\n\nExit reachable." : "\n\nEXIT NOT REACHABLE."), "OK");
                }
            }
            EditorGUILayout.HelpBox("Regenerating one layer only touches that layer's tiles/objects. Objects you add manually (without the GeneratedObject component) are never deleted.", MessageType.None);
        }

        private void Run(GenerationStage stages, string title)
        {
            string err;
            if (!_settings.Validate(out err)) { EditorUtility.DisplayDialog(title, err, "OK"); return; }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            int group = Undo.GetCurrentGroup();
            try
            {
                var routine = _gen.GenerateRoutine(stages, (p, msg) => EditorUtility.DisplayProgressBar(title, msg, p));
                while (routine.MoveNext()) { }
            }
            catch (System.Exception e) { Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); }
            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(_settings);
            EditorSceneManager.MarkSceneDirty(_gen.gameObject.scene);
            Repaint();
        }

        // ---------------------------------------------------------------- preview
        private void DrawPreview()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("PREVIEW", EditorStyles.boldLabel);
            _gen.showBounds = EditorGUILayout.Toggle("Show Bounds / Start / Exits", _gen.showBounds);
            _gen.previewMode = (PreviewMode)EditorGUILayout.EnumPopup("Mode", _gen.previewMode);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview (new data, no scene changes)"))
                {
                    string err;
                    if (_settings.Validate(out err)) { _gen.ShowPreview(_gen.previewMode, true); SceneView.RepaintAll(); }
                    else EditorUtility.DisplayDialog("Preview", err, "OK");
                }
                if (GUILayout.Button("Refresh Mode")) { _gen.ShowPreview(_gen.previewMode, false); SceneView.RepaintAll(); }
                if (GUILayout.Button("Hide")) { _gen.HidePreview(); SceneView.RepaintAll(); }
            }
            EditorGUILayout.HelpBox("White = start, magenta = exits. Preview builds data only; it does not overwrite your scene.", MessageType.None);
        }

        // ---------------------------------------------------------------- save / load
        private void DrawSaveLoad()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("SAVE / LOAD", EditorStyles.boldLabel);
            if (GUILayout.Button("Save Generated World (asset)"))
            {
                string dir = "Assets/TopDownWorldGenerator/Saves";
                System.IO.Directory.CreateDirectory(dir);
                string path = EditorUtility.SaveFilePanelInProject("Save World", "World_" + _settings.seed, "asset", "Choose a location", dir);
                if (!string.IsNullOrEmpty(path))
                {
                    var data = WorldSaveManager.Capture(_gen);
                    if (data == null) { EditorUtility.DisplayDialog("Save", "Nothing to save. Generate a world first.", "OK"); }
                    else
                    {
                        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
                        AssetDatabase.CreateAsset(data, path);
                        AssetDatabase.SaveAssets();
                        EditorGUIUtility.PingObject(data);
                        Debug.Log("[WorldGenerator] Saved world to " + path + " (" + data.objects.Count + " objects).");
                    }
                }
            }
            _loadData = (WorldSaveData)EditorGUILayout.ObjectField("Saved World", _loadData, typeof(WorldSaveData), false);
            using (new EditorGUI.DisabledScope(_loadData == null))
                if (GUILayout.Button("Load Saved World"))
                {
                    Undo.IncrementCurrentGroup();
                    int g = Undo.GetCurrentGroup();
                    WorldSaveManager.Load(_gen, _loadData);
                    Undo.CollapseUndoOperations(g);
                    EditorSceneManager.MarkSceneDirty(_gen.gameObject.scene);
                }
        }
    }
}
