using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TouchScript;
using TouchScript.Gestures;

// Presentation lives beside the board; it never controls the game rules.
public class ThrowPresentation : MonoBehaviour
{
    public enum Cue { Claim, Capture, Own, Win, Draw }
    readonly Color cyan = new Color(.63f,.96f,1f);
    readonly Color gold = new Color(1f,.79f,.40f);
    readonly Color ink = new Color(.055f,.12f,.22f,.98f);
    readonly Color muted = new Color(.82f,.88f,.95f);
    TMP_FontAsset hdFont;
    GameManager game;
    Canvas canvas;
    RectTransform hud;
    RectTransform restartRect;
    GameObject restartHit;
    GameObject calibrationBanner;
    TextMeshProUGUI turnLabel, feedback, symbolLabel, resultLabel, restartLabel, filledLabel;
    GameObject resultCard;
    Image playerAccent, symbolImage;
    Sprite xSprite,oSprite;
    AudioSource audioSource;
    readonly Dictionary<Cue,AudioClip> clips = new Dictionary<Cue,AudioClip>();
    readonly List<GameObject> effects = new List<GameObject>();
    LineRenderer winLine;
    Material lineMaterial;
    Camera sceneCamera;
    int screenWidth, screenHeight;
    ITouchManager touchManager;
    string detection = "Waiting for input";
    Vector2 lastPoint;
    bool lastAccepted;
    float detectedAt = -10;
    bool initialized;
    readonly Dictionary<int,Contact> contacts = new Dictionary<int,Contact>();
    struct Contact { public float time; public Vector2 start; public float travel; }

    public Color PlayerColor(int player) { return player == 1 ? cyan : gold; }
    public void Initialize(GameManager manager,Sprite x,Sprite o)
    {
        if (initialized) return;
        initialized = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#if UNITY_STANDALONE
        if (!Application.isEditor) Screen.SetResolution(1920,1080,FullScreenMode.ExclusiveFullScreen);
#endif
        game = manager; xSprite = x; oSprite = o;
        hdFont = Resources.Load<TMP_FontAsset>("TicTacThrow HD SDF");
        sceneCamera = Camera.main;
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        BuildHUD();
        BuildAudio();
        Layout();
    }
    RectTransform Box(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color? color = null)
    {
        var obj = new GameObject(name,typeof(RectTransform));
        var rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent,false); rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        if (color.HasValue)
        {
            var image = obj.AddComponent<Image>(); image.color = color.Value; image.raycastTarget = false;
        }
        return rt;
    }
    TextMeshProUGUI Label(string name,Transform parent,string content,float size,Color color,Vector2 min,Vector2 max,Vector2 low,Vector2 high,TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var rt = Box(name,parent,min,max,low,high);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (hdFont != null) label.font = hdFont;
        else if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.text = content; label.fontSize = size; label.color = color;
        label.alignment = align; label.raycastTarget = false;
        label.enableWordWrapping = true; label.enableAutoSizing = false;
        label.extraPadding = true;
        return label;
    }
    void BuildHUD()
    {
        var root = new GameObject("TicTacThrow HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        canvas.pixelPerfect = true;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1f;
        hud = root.GetComponent<RectTransform>();
        Label("Title",hud,"TIC TAC <color=#A0F5FF>THROW</color>",56,Color.white,new Vector2(.27f,.90f),new Vector2(.73f,.98f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        Label("Subtitle",hud,"AIM  /  CLAIM  /  CAPTURE",22,muted,new Vector2(.30f,.87f),new Vector2(.70f,.91f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        var rules = Box("How to play",hud,new Vector2(.025f,.18f),new Vector2(.225f,.82f),Vector2.zero,Vector2.zero,ink);
        Box("Rules accent",rules,new Vector2(0,0),new Vector2(.012f,1),Vector2.zero,Vector2.zero,cyan);
        Label("Rules heading",rules,"HOW TO PLAY",32,cyan,new Vector2(.09f,.85f),new Vector2(.92f,.96f),Vector2.zero,Vector2.zero);
        Label("Claim",rules,"<b><color=#A0F5FF>01  CLAIM</color></b>\nHit an empty square\nto place your symbol.",28,Color.white,new Vector2(.09f,.60f),new Vector2(.92f,.82f),Vector2.zero,Vector2.zero);
        Label("Capture",rules,"<b><color=#A0F5FF>02  CAPTURE</color></b>\nHit your opponent's\nsquare to steal it.",28,Color.white,new Vector2(.09f,.36f),new Vector2(.92f,.58f),Vector2.zero,Vector2.zero);
        Label("Win",rules,"<b><color=#A0F5FF>03  CONNECT</color></b>\nGet three in a row\nto win.",28,Color.white,new Vector2(.09f,.18f),new Vector2(.92f,.34f),Vector2.zero,Vector2.zero);
        Label("Own square rule",rules,"Already yours? Throw again.\nFull board? A tie if no winner.",22,muted,new Vector2(.09f,.025f),new Vector2(.92f,.15f),Vector2.zero,Vector2.zero);
        var player = Box("Player panel",hud,new Vector2(.775f,.46f),new Vector2(.975f,.78f),Vector2.zero,Vector2.zero,ink);
        playerAccent = Box("Player accent",player,new Vector2(0,0),new Vector2(.012f,1),Vector2.zero,Vector2.zero,cyan).GetComponent<Image>();
        turnLabel = Label("Turn",player,"GET READY",32,cyan,new Vector2(.06f,.78f),new Vector2(.94f,.96f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        symbolImage = Box("Player symbol",player,new Vector2(.34f,.31f),new Vector2(.66f,.71f),Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
        symbolImage.preserveAspect = true; symbolImage.raycastTarget = false;
        symbolLabel = Label("Fallback symbol",player,"X",90,cyan,new Vector2(.24f,.28f),new Vector2(.76f,.72f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        feedback = Label("Feedback",player,"Board preparing",28,Color.white,new Vector2(.06f,.06f),new Vector2(.94f,.26f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        filledLabel = Label("Board progress",hud,"0 / 9 SQUARES FILLED",24,muted,new Vector2(.29f,.07f),new Vector2(.71f,.12f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        resultCard = Box("Round result",hud,new Vector2(.775f,.22f),new Vector2(.975f,.43f),Vector2.zero,Vector2.zero,ink).gameObject;
        resultLabel = Label("Result",resultCard.transform,"",38,cyan,new Vector2(.06f,.61f),new Vector2(.94f,.95f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        restartRect = Box("Restart target",resultCard.transform,new Vector2(.08f,.09f),new Vector2(.92f,.52f),Vector2.zero,Vector2.zero,new Color(.12f,.28f,.38f));
        restartLabel = Label("Restart text",restartRect,"RESULT LOCKED",28,cyan,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4),TextAlignmentOptions.Center);
        // TouchScript's scene layer already hits screen-space UI. Put the gesture
        // on the visible target itself so mouse and TUIO share the same bounds.
        restartRect.GetComponent<Image>().raycastTarget = true;
        var button = restartRect.gameObject.AddComponent<Button>(); button.targetGraphic = restartRect.GetComponent<Image>();
        button.onClick.AddListener(() => game.OnRestart());
        restartHit = restartRect.gameObject;
        restartHit.AddComponent<TapGesture>().enabled = false;
        restartHit.AddComponent<StartGame>();
        resultCard.SetActive(false);
        Label("Operator hint",hud,"F1  OPERATOR CALIBRATION",18,muted,new Vector2(.78f,.02f),new Vector2(.975f,.06f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Right);
        calibrationBanner = Box("Calibration banner",hud,new Vector2(.3f,.82f),new Vector2(.7f,.86f),Vector2.zero,Vector2.zero,ink).gameObject;
        Label("Calibration label",calibrationBanner.transform,"CALIBRATION ACTIVE  /  PLAY PAUSED",24,gold,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        calibrationBanner.SetActive(false);
    }
    void Layout()
    {
        screenWidth = Screen.width; screenHeight = Screen.height;
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            // Reserve the middle 50% for the board, with title/footer clearance.
            sceneCamera.orthographicSize = Mathf.Max(6.4f,10.2f / Mathf.Max(.5f,sceneCamera.aspect));
            var backdrop = GameObject.Find("background_1");
            if (backdrop != null)
            {
                var sprite = backdrop.GetComponent<SpriteRenderer>();
                if (sprite != null && sprite.sprite != null)
                {
                    Vector3 size = sprite.sprite.bounds.size;
                    float cover = Mathf.Max(2*sceneCamera.orthographicSize/size.y,2*sceneCamera.orthographicSize*sceneCamera.aspect/size.x);
                    backdrop.transform.localScale = new Vector3(cover,cover,1);
                    backdrop.transform.position = new Vector3(sceneCamera.transform.position.x,sceneCamera.transform.position.y,backdrop.transform.position.z);
                }
            }
        }
        Canvas.ForceUpdateCanvases();
    }
    void Update()
    {
        if (!initialized || game == null) return;
        if (screenWidth != Screen.width || screenHeight != Screen.height) Layout();
        if (touchManager == null && TouchManager.Instance != null)
        {
            touchManager = TouchManager.Instance;
            touchManager.PointersPressed += ContactStarted;
            touchManager.PointersUpdated += ContactUpdated;
            touchManager.PointersRemoved += ContactEnded;
            touchManager.PointersCancelled += ContactEnded;
        }
        filledLabel.text = game.Rules.Filled + " / 9 SQUARES FILLED";
        bool ready = game.RestartReady;
        if (resultCard.activeSelf)
        {
            restartLabel.text = ready ? "THROW HERE\nTO PLAY AGAIN" : game.RestartWait > 0 ? "RESULT LOCKED\n" + Mathf.CeilToInt(game.RestartWait) + " SECONDS" : "CLEAR SURFACE\nTO CONTINUE";
            restartRect.GetComponent<Button>().interactable = ready;
            restartHit.GetComponent<TapGesture>().enabled = ready;
        }
    }
    public void ConfigureRestartInput(float duration,float distance)
    {
        if (restartHit == null) return;
        var tap = restartHit.GetComponent<TapGesture>(); tap.TimeLimit = duration; tap.DistanceLimit = distance;
    }
    public void ShowPreparing()
    {
        restartHit.GetComponent<TapGesture>().enabled = false;
        resultCard.SetActive(false);
        turnLabel.text = "GET READY"; feedback.text = "Clear the board to begin";
        SetPlayer(1);
    }
    void SetPlayer(int player)
    {
        Color color = PlayerColor(player);
        playerAccent.color = turnLabel.color = symbolLabel.color = color;
        symbolImage.sprite = player == 1 ? xSprite : oSprite;
        symbolImage.color = color; symbolImage.enabled = symbolImage.sprite != null;
        symbolLabel.gameObject.SetActive(!symbolImage.enabled);
        symbolLabel.text = player == 1 ? "X" : "O";
    }
    public void ShowTurn(int player)
    {
        SetPlayer(player); turnLabel.text = player == 1 ? "PLAYER X'S TURN" : "PLAYER O'S TURN";
        feedback.text = "Aim for a square"; feedback.color = muted;
    }
    public void ShowFeedback(int player,string message,bool own)
    {
        SetPlayer(player); turnLabel.text = own ? "THROW AGAIN" : "HIT CONFIRMED";
        feedback.text = message; feedback.color = own ? gold : PlayerColor(player);
    }
    public void ShowResult(int player,bool draw)
    {
        SetPlayer(player);
        turnLabel.text = draw ? "ROUND COMPLETE" : "THREE IN A ROW!";
        feedback.text = draw ? "All nine squares filled" : "Nice aim. Great capture.";
        resultLabel.text = draw ? "IT'S A TIE!" : (player == 1 ? "X WINS!" : "O WINS!");
        resultLabel.color = draw ? cyan : PlayerColor(player);
        resultCard.SetActive(true);
    }
    public void ShowCalibration(bool show) { if (calibrationBanner != null) calibrationBanner.SetActive(show); }
    LineRenderer CreateLine(string name,Color color,int count,float width)
    {
        var obj = new GameObject(name); obj.transform.SetParent(transform,false);
        var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true; line.positionCount = count; line.widthMultiplier = width;
        line.startColor = line.endColor = color; line.sortingOrder = 25;
        effects.Add(obj); return line;
    }
    public void HitEffect(Vector3 position,int player,bool own)
    {
        var ring = CreateLine("Throw impact",own ? gold : PlayerColor(player),33,.065f);
        StartCoroutine(ExpandRing(ring,position));
    }
    IEnumerator ExpandRing(LineRenderer line,Vector3 center)
    {
        float start = Time.unscaledTime;
        Color color = line.startColor;
        while (Time.unscaledTime - start < .45f)
        {
            if (line == null) yield break;
            float t = (Time.unscaledTime-start)/.45f;
            for (int i = 0; i <= 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                line.SetPosition(i,center + new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),-.1f) * Mathf.Lerp(.25f,1.35f,t));
            }
            color.a = 1-t; line.startColor = line.endColor = color;
            yield return null;
        }
        if (line != null) { effects.Remove(line.gameObject); Destroy(line.gameObject); }
    }
    public void ShowWinningLine(List<int> cells,List<TileHandler> tiles,int player)
    {
        if (cells.Count < 3) return;
        var first = tiles.Find(t => t != null && t.TileNumber == cells[0]);
        var last = tiles.Find(t => t != null && t.TileNumber == cells[2]);
        if (first == null || last == null) return;
        Vector3 a = first.GetSymbolSpriteRenderer().transform.position;
        Vector3 b = last.GetSymbolSpriteRenderer().transform.position;
        Vector3 extra = (b-a).normalized * .6f; a -= extra; b += extra; a.z = b.z = -.15f;
        winLine = CreateLine("Winning connection",PlayerColor(player),2,.12f);
        winLine.SetPosition(0,a); winLine.SetPosition(1,b);
    }
    public void ClearEffects()
    {
        StopAllCoroutines();
        foreach (var effect in effects) if (effect != null) Destroy(effect);
        effects.Clear(); winLine = null;
        if (audioSource != null) audioSource.Stop();
    }
    void BuildAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0; audioSource.volume = .24f;
        clips[Cue.Claim] = Tone("Claim",new float[]{660,880},.09f);
        clips[Cue.Capture] = Tone("Capture",new float[]{440,660,990},.085f);
        clips[Cue.Own] = Tone("Already yours",new float[]{240,200},.08f);
        clips[Cue.Win] = Tone("Victory",new float[]{523,659,784,1046},.14f);
        clips[Cue.Draw] = Tone("Tie",new float[]{440,392,440},.13f);
    }
    AudioClip Tone(string name,float[] notes,float noteDuration)
    {
        const int rate = 44100;
        int segment = (int)(rate*noteDuration); float[] data = new float[segment*notes.Length];
        for (int n = 0; n < notes.Length; n++) for (int i = 0; i < segment; i++)
        {
            float t = (float)i/rate;
            float envelope = Mathf.Min(1,t/.008f) * Mathf.Clamp01((noteDuration-t)/.035f);
            data[n*segment+i] = Mathf.Sin(2*Mathf.PI*notes[n]*t) * envelope * .35f;
        }
        var clip = AudioClip.Create(name,data.Length,1,rate,false); clip.SetData(data,0); return clip;
    }
    public void PlayCue(Cue cue) { if (audioSource != null && clips.ContainsKey(cue)) audioSource.PlayOneShot(clips[cue]); }
    public void RecordDetection(Vector2 point,bool accepted,string reason)
    { lastPoint = point; lastAccepted = accepted; detection = reason; detectedAt = Time.unscaledTime; }
    void ContactStarted(object sender,PointerEventArgs args)
    {
        foreach (var pointer in args.Pointers)
            contacts[pointer.Id] = new Contact { time = Time.unscaledTime, start = pointer.Position };
    }
    void ContactUpdated(object sender,PointerEventArgs args)
    {
        foreach (var pointer in args.Pointers)
        {
            Contact contact;
            if (!contacts.TryGetValue(pointer.Id,out contact)) continue;
            contact.travel = Mathf.Max(contact.travel,Vector2.Distance(contact.start,pointer.Position));
            contacts[pointer.Id] = contact;
        }
    }
    void ContactEnded(object sender,PointerEventArgs args)
    {
        foreach (var pointer in args.Pointers)
        {
            Contact contact;
            if (contacts.TryGetValue(pointer.Id,out contact))
            {
                float travel = Mathf.Max(contact.travel,Vector2.Distance(contact.start,pointer.Position));
                if (Time.unscaledTime-contact.time > game.maximumContactTime)
                    RecordDetection(pointer.Position,false,"Rejected: contact held too long");
                else if (travel > game.maximumTravelCentimeters*touchManager.DotsPerCentimeter)
                    RecordDetection(pointer.Position,false,"Rejected: contact moved too far");
                else if (Time.unscaledTime-detectedAt > .1f)
                    RecordDetection(pointer.Position,false,"No target hit (outside board or filtered)");
            }
            contacts.Remove(pointer.Id);
        }
    }
    void OnGUI()
    {
        if (!initialized || game == null || !game.CalibrationOpen) return;
        float scale = Mathf.Max(.65f,Mathf.Min(Screen.width/1920f,Screen.height/1080f));
        Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
        GUI.Box(new Rect(16,16,470,350),"OPERATOR CALIBRATION  /  F1 TO CLOSE");
        GUI.Label(new Rect(32,46,438,32),"TUIO port 3333 | Active points: " + (touchManager != null ? touchManager.PointersCount : 0));
        GUI.Label(new Rect(32,80,438,32),"Last detection: " + detection);
        GUI.Label(new Rect(32,116,438,24),"Contact limit: " + game.maximumContactTime.ToString("0.00") + " sec");
        game.maximumContactTime = GUI.HorizontalSlider(new Rect(32,145,430,20),game.maximumContactTime,.15f,2f);
        GUI.Label(new Rect(32,172,438,24),"Movement limit: " + game.maximumTravelCentimeters.ToString("0.0") + " cm");
        game.maximumTravelCentimeters = GUI.HorizontalSlider(new Rect(32,201,430,20),game.maximumTravelCentimeters,.5f,10f);
        GUI.Label(new Rect(32,228,438,24),"Between throws: " + game.minimumThrowInterval.ToString("0.00") + " sec");
        game.minimumThrowInterval = GUI.HorizontalSlider(new Rect(32,257,430,20),game.minimumThrowInterval,.3f,1.5f);
        if (GUI.Button(new Rect(32,295,205,40),"APPLY + SAVE")) game.SaveInputSettings();
        if (GUI.Button(new Rect(250,295,212,40),"RESTORE DEFAULTS"))
        { game.maximumContactTime = .8f; game.maximumTravelCentimeters = 4f; game.minimumThrowInterval = .65f; game.SaveInputSettings(); }
        GUI.matrix = old;
        if (sceneCamera == null) return;
        foreach (var tile in game.Tiles)
        {
            if (tile == null) continue;
            var collider = tile.GetComponent<Collider>(); if (collider == null) continue;
            var bounds = collider.bounds;
            Vector3 min = sceneCamera.WorldToScreenPoint(bounds.min), max = sceneCamera.WorldToScreenPoint(bounds.max);
            var rect = new Rect(min.x,Screen.height-max.y,max.x-min.x,max.y-min.y);
            Outline(rect,cyan); GUI.Label(new Rect(rect.x+8,rect.y+8,80,25),"TILE " + (tile.TileNumber+1));
        }
        if (touchManager != null) foreach (var pointer in touchManager.Pointers)
        {
            Contact contact; string label = "ID " + pointer.Id;
            if (contacts.TryGetValue(pointer.Id,out contact)) label += " / " + (Time.unscaledTime-contact.time).ToString("0.00") + "s";
            Dot(pointer.Position,cyan,label);
        }
        if (Time.unscaledTime-detectedAt < 2f) Dot(lastPoint,lastAccepted ? Color.green : gold,lastAccepted ? "ACCEPTED" : "REJECTED");
    }
    void Outline(Rect rect,Color color)
    {
        Color old = GUI.color; GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,rect.yMax-2,rect.width,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,rect.y,2,rect.height),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax-2,rect.y,2,rect.height),Texture2D.whiteTexture); GUI.color = old;
    }
    void Dot(Vector2 point,Color color,string label)
    {
        Color old = GUI.color; GUI.color = color;
        GUI.DrawTexture(new Rect(point.x-6,Screen.height-point.y-6,12,12),Texture2D.whiteTexture);
        GUI.Label(new Rect(point.x+12,Screen.height-point.y-14,220,30),label); GUI.color = old;
    }
    void OnDisable()
    {
        if (touchManager != null)
        {
            touchManager.PointersPressed -= ContactStarted; touchManager.PointersUpdated -= ContactUpdated;
            touchManager.PointersRemoved -= ContactEnded; touchManager.PointersCancelled -= ContactEnded;
            touchManager = null;
        }
        ClearEffects(); contacts.Clear();
    }
    void OnDestroy()
    {
        foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
