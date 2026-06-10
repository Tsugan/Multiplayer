using TMPro;
using UnityEngine;

namespace Practice1
{
    public class FinalProjectSceneBootstrap : MonoBehaviour
    {
        private static AudioSource _audioSource;
        private static AudioClip _shotClip;
        private static AudioClip _pickupClip;
        private static AudioClip _throwClip;
        private static AudioClip _disposeClip;
        private static AudioClip _explodeClip;

        private GameObject _arenaRoot;
        private GameObject _bombVisual;
        private GameObject _disposalZoneVisual;
        private TextMeshProUGUI _fpsText;
        private int _lastBombEventId;
        private float _fpsTimer;
        private int _fpsFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<FinalProjectSceneBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrap = new GameObject("FinalProjectSceneBootstrap");
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<FinalProjectSceneBootstrap>();
        }

        private void Awake()
        {
            EnsureAudio();
            CreateArenaPresentation();
            if (!Application.isBatchMode)
            {
                CreateHudAdditions();
            }
        }

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (!Application.isBatchMode)
            {
                UpdateBombVisual(manager);
                UpdateFpsText();
            }

            if (manager.BombEventId != _lastBombEventId)
            {
                _lastBombEventId = manager.BombEventId;
                PlayBombEvent(manager.BombEventType, manager.BombPosition);
            }
        }

        public static void PlayShotSound(Vector3 position)
        {
            PlayClip(_shotClip, position, 0.28f);
        }

        private void CreateArenaPresentation()
        {
            if (_arenaRoot != null)
            {
                return;
            }

            _arenaRoot = new GameObject("BombDisposalArenaPresentation");
            Material floor = CreateMaterial("ArenaFloor", new Color(0.16f, 0.18f, 0.18f));
            Material wall = CreateMaterial("ArenaWalls", new Color(0.08f, 0.09f, 0.10f));
            Material cover = CreateMaterial("ArenaCover", new Color(0.25f, 0.28f, 0.31f));
            Material hazard = CreateMaterial("HazardStripes", new Color(0.95f, 0.72f, 0.1f));
            Material disposal = CreateMaterial("DisposalZone", new Color(0.1f, 0.75f, 0.95f));
            Material bomb = CreateMaterial("BombCore", new Color(1f, 0.16f, 0.08f));

            CreateCube("Floor", new Vector3(0f, -0.12f, 1f), new Vector3(20f, 0.2f, 24f), floor);
            CreateCube("NorthWall", new Vector3(0f, 1f, 13f), new Vector3(21f, 2f, 0.5f), wall);
            CreateCube("SouthWall", new Vector3(0f, 1f, -11f), new Vector3(21f, 2f, 0.5f), wall);
            CreateCube("WestWall", new Vector3(-10f, 1f, 1f), new Vector3(0.5f, 2f, 24f), wall);
            CreateCube("EastWall", new Vector3(10f, 1f, 1f), new Vector3(0.5f, 2f, 24f), wall);

            CreateCube("Cover_A", new Vector3(-4.5f, 0.55f, -2f), new Vector3(3f, 1.1f, 1.1f), cover);
            CreateCube("Cover_B", new Vector3(4.5f, 0.55f, 4f), new Vector3(3f, 1.1f, 1.1f), cover);
            CreateCube("Cover_C", new Vector3(-4f, 0.55f, 6.5f), new Vector3(1.2f, 1.1f, 3.2f), cover);
            CreateCube("Cover_D", new Vector3(4f, 0.55f, -5f), new Vector3(1.2f, 1.1f, 3.2f), cover);

            CreateCube("BombSpawnMark", new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.05f, 2.2f), hazard);

            _disposalZoneVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _disposalZoneVisual.name = "DisposalZoneVisual";
            _disposalZoneVisual.transform.SetParent(_arenaRoot.transform);
            _disposalZoneVisual.transform.position = new Vector3(0f, 0.04f, 8f);
            _disposalZoneVisual.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
            _disposalZoneVisual.GetComponent<Renderer>().material = disposal;
            Destroy(_disposalZoneVisual.GetComponent<Collider>());

            _bombVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _bombVisual.name = "BombVisual";
            _bombVisual.transform.SetParent(_arenaRoot.transform);
            _bombVisual.transform.localScale = Vector3.one * 0.8f;
            _bombVisual.GetComponent<Renderer>().material = bomb;
            Destroy(_bombVisual.GetComponent<Collider>());

            Light bombLight = _bombVisual.AddComponent<Light>();
            bombLight.type = LightType.Point;
            bombLight.range = 6f;
            bombLight.intensity = 2f;
            bombLight.color = new Color(1f, 0.25f, 0.1f);

            Light key = new GameObject("ArenaKeyLight").AddComponent<Light>();
            key.transform.SetParent(_arenaRoot.transform);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            key.intensity = 1.25f;
        }

        private void CreateHudAdditions()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || _fpsText != null)
            {
                return;
            }

            GameObject fpsObject = new GameObject("FpsCounter", typeof(RectTransform), typeof(TextMeshProUGUI));
            fpsObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = fpsObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -12f);
            rect.sizeDelta = new Vector2(220f, 32f);

            _fpsText = fpsObject.GetComponent<TextMeshProUGUI>();
            _fpsText.alignment = TextAlignmentOptions.TopRight;
            _fpsText.fontSize = 16f;
            _fpsText.color = new Color(0.85f, 1f, 0.95f);
            _fpsText.raycastTarget = false;
        }

        private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(_arenaRoot.transform);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().material = material;
            return cube;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Standard");
            Material material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            material.color = color;
            return material;
        }

        private void UpdateBombVisual(GameManager manager)
        {
            if (_bombVisual == null || _disposalZoneVisual == null)
            {
                return;
            }

            _disposalZoneVisual.transform.position = manager.DisposalZonePosition;
            bool visible = manager.CurrentBombPhase != BombPhase.Respawning;
            _bombVisual.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _bombVisual.transform.position = manager.BombPosition + Vector3.up * 0.35f;
            float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.08f;
            _bombVisual.transform.localScale = Vector3.one * (0.8f * pulse);
        }

        private void UpdateFpsText()
        {
            if (_fpsText == null)
            {
                return;
            }

            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer < 0.5f)
            {
                return;
            }

            float fps = _fpsFrames / Mathf.Max(0.001f, _fpsTimer);
            _fpsText.text = $"FPS: {fps:0}";
            _fpsTimer = 0f;
            _fpsFrames = 0;
        }

        private static void EnsureAudio()
        {
            if (_audioSource != null)
            {
                return;
            }

            GameObject audioObject = new GameObject("FinalProjectAudio");
            DontDestroyOnLoad(audioObject);
            _audioSource = audioObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0.35f;
            _audioSource.volume = 0.65f;

            _shotClip = CreateTone("Shot", 820f, 0.07f, 0.18f);
            _pickupClip = CreateTone("Pickup", 520f, 0.12f, 0.22f);
            _throwClip = CreateTone("Throw", 300f, 0.10f, 0.18f);
            _disposeClip = CreateTone("Dispose", 660f, 0.22f, 0.25f);
            _explodeClip = CreateNoise("Explosion", 0.45f, 0.35f);
        }

        private static void PlayBombEvent(int eventType, Vector3 position)
        {
            switch (eventType)
            {
                case 1:
                case 5:
                    PlayClip(_pickupClip, position, 0.45f);
                    break;
                case 2:
                    PlayClip(_throwClip, position, 0.42f);
                    break;
                case 3:
                    PlayClip(_disposeClip, position, 0.55f);
                    break;
                case 4:
                    PlayClip(_explodeClip, position, 0.75f);
                    break;
            }
        }

        private static void PlayClip(AudioClip clip, Vector3 position, float volume)
        {
            EnsureAudio();
            if (clip == null || _audioSource == null)
            {
                return;
            }

            _audioSource.transform.position = position;
            _audioSource.PlayOneShot(clip, volume);
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float volume)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - i / (float)samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoise(string name, float duration, float volume)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float envelope = 1f - i / (float)samples;
                data[i] = Random.Range(-1f, 1f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
