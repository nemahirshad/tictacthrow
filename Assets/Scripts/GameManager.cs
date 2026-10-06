using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TouchScript;

public class GameManager : MonoBehaviour
{
    [Header("Game Elements")]
    [SerializeField] GameObject board;
    [SerializeField] GameObject endGameScreen;
    [SerializeField] Text text;
    [Header("Board Lines Animation")]
    [SerializeField] List<GameObject> horizontalBoardLines;
    [SerializeField] List<GameObject> verticalBoardLines;
    [SerializeField] float lineAnimationDuration = .4f;
    [SerializeField] float animationDelayBetweenLines = .1f;
    [SerializeField] Vector3 targetHorizontalLineScale = Vector3.one;
    [SerializeField] Vector3 targetVerticalLineScale = Vector3.one;
    [SerializeField] LeanTweenType easeType = LeanTweenType.easeOutExpo;
    [Header("Symbol Animations")]
    [SerializeField] float popInAnimationDuration = .4f;
    [SerializeField] LeanTweenType popInEaseType = LeanTweenType.easeOutBack;
    [SerializeField] float flipDurationPart1 = .2f;
    [SerializeField] float flipDurationPart2 = .2f;
    [SerializeField] LeanTweenType flipEase = LeanTweenType.easeInOutSine;
    [SerializeField] Vector3 symbolTargetScale = Vector3.one;
    [Header("Gameplay Sprites & Logic")]
    [SerializeField] Sprite xSprite;
    [SerializeField] Sprite oSprite;
    [SerializeField] List<TileHandler> tileHandlers = new List<TileHandler>();
    [Header("Winning Feedback")]
    [SerializeField] Color flashColor = new Color(.63f,.96f,1f);
    [SerializeField] float flashToColorDuration = .1f;
    [SerializeField] float flashToOriginalDuration = .15f;
    [SerializeField] int numberOfFlashes = 3;
    [SerializeField] float delayBeforeEndScreen = .5f;
    [SerializeField] TextMeshProUGUI turnIndicatorText;
    [Header("Throw Filtering (F1 opens operator calibration)")]
    [Min(.1f)] public float minimumThrowInterval = .65f;
    [Min(.05f)] public float clearSurfaceDuration = .18f;
    [Range(.15f,2f)] public float maximumContactTime = .8f;
    [Range(.5f,10f)] public float maximumTravelCentimeters = 4f;
    [Min(1f)] public float resultProtectionDuration = 2f;

    public enum RoundState { Preparing, Playing, Resolving, Results }
    public RoundState State { get; private set; }
    public bool canPlay { get { return State == RoundState.Playing && !CalibrationOpen; } }
    public bool CalibrationOpen { get; private set; }
    public bool RestartReady { get { return State == RoundState.Results && !CalibrationOpen && restartArmed; } }
    public float RestartWait { get { return Mathf.Max(0,restartAt-Time.unscaledTime); } }
    public TileHandler[] Tiles { get { return tileHandlers.ToArray(); } }
    public ThrowBoard Rules { get { return rules; } }
    public string LastDetection { get; private set; } = "Waiting for input";
    public static GameManager instance;
    public delegate void OnTileHit(SpriteRenderer renderer, int tileNum);
    public static OnTileHit tileHit;
    readonly ThrowBoard rules = new ThrowBoard();
    ThrowPresentation presentation;
    float restartAt;
    bool restartArmed;
    float lastContactAt;
    ITouchManager touchManager;
    bool subscribed;
    Sprite fallbackLineSprite;
    bool SurfaceClear { get { return Time.unscaledTime - lastContactAt >= clearSurfaceDuration; } }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        minimumThrowInterval = PlayerPrefs.GetFloat("Throw.Interval",minimumThrowInterval);
        maximumContactTime = PlayerPrefs.GetFloat("Throw.Contact",maximumContactTime);
        maximumTravelCentimeters = PlayerPrefs.GetFloat("Throw.Travel",maximumTravelCentimeters);
    }
    void OnEnable()
    {
        if (instance != this) return;
        tileHit += HandleTileHit;
        subscribed = true;
        if (presentation != null) BeginRound();
    }
    void Start()
    {
        if (instance != this) return;
        presentation = GetComponent<ThrowPresentation>();
        if (presentation == null) presentation = gameObject.AddComponent<ThrowPresentation>();
        presentation.Initialize(this, xSprite, oSprite);
        if (turnIndicatorText != null) turnIndicatorText.gameObject.SetActive(false);
        // Replace the old full-screen restart hit area with the dedicated HUD target.
        if (endGameScreen != null) endGameScreen.SetActive(false);
        ApplyInputSettings();
        BeginRound();
    }
    void Update()
    {
        if (instance != this) return;
        if (Input.GetKeyDown(KeyCode.F1))
        {
            CalibrationOpen = !CalibrationOpen;
            restartArmed = false;
            lastContactAt = Time.unscaledTime;
            if (presentation != null) presentation.ShowCalibration(CalibrationOpen);
        }
        if (touchManager == null) touchManager = TouchManager.Instance;
        if (touchManager != null && touchManager.PressedPointersCount > 0)
            lastContactAt = Time.unscaledTime;
        // Arm once on a clear surface. Keep the collider active through the next
        // contact, otherwise disabling it on pointer-down would cancel the tap.
        if (State == RoundState.Results && !CalibrationOpen && Time.unscaledTime >= restartAt && SurfaceClear)
            restartArmed = true;
    }
    void OnDisable()
    {
        if (subscribed) { tileHit -= HandleTileHit; subscribed = false; }
        if (instance != this) return;
        StopAllCoroutines();
        CancelAnimations();
        State = RoundState.Preparing;
        restartArmed = false;
    }
    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (fallbackLineSprite != null) Destroy(fallbackLineSprite);
    }
    public void ApplyInputSettings()
    {
        foreach (var tile in tileHandlers)
            if (tile != null) tile.ConfigureInput(maximumContactTime, maximumTravelCentimeters);
        if (presentation != null) presentation.ConfigureRestartInput(maximumContactTime, maximumTravelCentimeters);
    }
    public void SaveInputSettings()
    {
        PlayerPrefs.SetFloat("Throw.Interval",minimumThrowInterval);
        PlayerPrefs.SetFloat("Throw.Contact",maximumContactTime);
        PlayerPrefs.SetFloat("Throw.Travel",maximumTravelCentimeters);
        PlayerPrefs.Save();
        ApplyInputSettings();
    }
    void CancelAnimations()
    {
        if (horizontalBoardLines != null) foreach (var line in horizontalBoardLines) if (line != null) LeanTween.cancel(line);
        if (verticalBoardLines != null) foreach (var line in verticalBoardLines) if (line != null) LeanTween.cancel(line);
        foreach (var tile in tileHandlers)
            if (tile != null && tile.GetSymbolSpriteRenderer() != null) LeanTween.cancel(tile.GetSymbolSpriteRenderer().gameObject);
        if (presentation != null) presentation.ClearEffects();
    }
    void BeginRound()
    {
        State = RoundState.Preparing;
        StopAllCoroutines();
        CancelAnimations();
        rules.Reset();
        foreach (var tile in tileHandlers) if (tile != null) tile.CleanUp();
        if (board != null) board.SetActive(true);
        if (endGameScreen != null) endGameScreen.SetActive(false);
        presentation.ShowPreparing();
        float delay = 0f;
        AnimateLines(horizontalBoardLines, true, ref delay);
        AnimateLines(verticalBoardLines, false, ref delay);
        StartCoroutine(ReadyAfterEntry(delay > 0 ? delay - animationDelayBetweenLines + lineAnimationDuration : 0));
    }
    void AnimateLines(List<GameObject> lines, bool horizontal, ref float delay)
    {
        if (lines == null) return;
        foreach (var line in lines)
        {
            if (line == null) continue;
            line.SetActive(true);
            var renderer = line.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite == null)
            {
                // Keep the grid visible even if Unity's package-owned Square asset
                // is unavailable after moving or importing the project.
                if (fallbackLineSprite == null)
                    fallbackLineSprite = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
                renderer.sprite = fallbackLineSprite;
            }
            var target = horizontal ? targetHorizontalLineScale : targetVerticalLineScale;
            line.transform.localScale = horizontal ? new Vector3(0,target.y,target.z) : new Vector3(target.x,0,target.z);
            var tween = horizontal ? LeanTween.scaleX(line,target.x,lineAnimationDuration) : LeanTween.scaleY(line,target.y,lineAnimationDuration);
            tween.setEase(easeType).setDelay(delay);
            delay += animationDelayBetweenLines;
        }
    }
    IEnumerator ReadyAfterEntry(float duration)
    {
        yield return new WaitForSeconds(duration);
        lastContactAt = Time.unscaledTime;
        while (!SurfaceClear || CalibrationOpen) yield return null;
        State = RoundState.Playing;
        presentation.ShowTurn(rules.Player);
    }
    void HandleTileHit(SpriteRenderer renderer, int tileNum)
    {
        var tile = tileHandlers.Find(t => t != null && t.TileNumber == tileNum);
        if (tile != null) ReceiveHit(tile, renderer != null ? (Vector2)Camera.main.WorldToScreenPoint(renderer.transform.position) : Vector2.zero);
    }
    public void ReceiveHit(TileHandler tile, Vector2 position)
    {
        if (tile == null || tile.GetSymbolSpriteRenderer() == null) return;
        if (!canPlay)
        {
            RecordHit(position, false, CalibrationOpen ? "Calibration active" : "Wait for the next turn");
            return;
        }
        // Close the gate synchronously before any animation, to reject same-frame duplicates.
        State = RoundState.Resolving;
        lastContactAt = Time.unscaledTime;
        int player = rules.Player;
        var move = rules.Apply(tile.TileNumber);
        if (move == ThrowBoard.Move.Invalid) { State = RoundState.Playing; return; }
        bool own = move == ThrowBoard.Move.OwnSquare;
        RecordHit(position, !own, own ? "Already yours - throw again" : move == ThrowBoard.Move.Captured ? "Captured!" : "Square claimed!");
        var sr = tile.GetSymbolSpriteRenderer();
        presentation.HitEffect(sr.transform.position, player, own);
        presentation.PlayCue(own ? ThrowPresentation.Cue.Own : move == ThrowBoard.Move.Captured ? ThrowPresentation.Cue.Capture : ThrowPresentation.Cue.Claim);
        presentation.ShowFeedback(player, own ? "ALREADY YOURS" : move == ThrowBoard.Move.Captured ? "CAPTURED!" : "SQUARE CLAIMED", own);
        float duration = .3f;
        if (!own)
        {
            LeanTween.cancel(sr.gameObject);
            sr.color = presentation.PlayerColor(player);
            var sprite = player == 1 ? xSprite : oSprite;
            if (move == ThrowBoard.Move.Claimed)
            {
                sr.sprite = sprite;
                sr.transform.localScale = Vector3.zero;
                LeanTween.scale(sr.gameObject,symbolTargetScale,popInAnimationDuration).setEase(popInEaseType);
                duration = popInAnimationDuration;
            }
            else
            {
                sr.transform.localScale = symbolTargetScale;
                LeanTween.scaleY(sr.gameObject,0,flipDurationPart1).setEase(flipEase).setOnComplete(() =>
                {
                    sr.sprite = sprite;
                    LeanTween.scaleY(sr.gameObject,symbolTargetScale.y,flipDurationPart2).setEase(flipEase);
                });
                duration = flipDurationPart1 + flipDurationPart2;
            }
        }
        StartCoroutine(ResolveMove(duration));
    }
    void RecordHit(Vector2 position, bool accepted, string reason)
    {
        LastDetection = reason;
        if (presentation != null) presentation.RecordDetection(position,accepted,reason);
    }
    IEnumerator ResolveMove(float animationDuration)
    {
        yield return new WaitForSeconds(animationDuration);
        if (rules.Finished)
        {
            State = RoundState.Results;
            // Protect results immediately, then again when the result panel appears.
            restartAt = float.PositiveInfinity;
            restartArmed = false;
            if (!rules.Draw)
            {
                presentation.ShowWinningLine(rules.WinningCells, tileHandlers, rules.Player);
                foreach (int index in rules.WinningCells)
                {
                    var tile = tileHandlers.Find(t => t != null && t.TileNumber == index);
                    if (tile != null) StartCoroutine(FlashWinner(tile.GetSymbolSpriteRenderer()));
                }
                presentation.PlayCue(ThrowPresentation.Cue.Win);
                yield return new WaitForSeconds(numberOfFlashes * (flashToColorDuration + flashToOriginalDuration) + delayBeforeEndScreen);
            }
            else presentation.PlayCue(ThrowPresentation.Cue.Draw);
            restartAt = Time.unscaledTime + resultProtectionDuration;
            lastContactAt = Time.unscaledTime;
            presentation.ShowResult(rules.Player,rules.Draw);
            yield break;
        }
        float remaining = Mathf.Max(0, minimumThrowInterval - animationDuration);
        yield return new WaitForSecondsRealtime(remaining);
        while (!SurfaceClear || CalibrationOpen) yield return null;
        State = RoundState.Playing;
        presentation.ShowTurn(rules.Player);
    }
    IEnumerator FlashWinner(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        for (int i = 0; i < numberOfFlashes; i++)
        {
            LeanTween.color(sr.gameObject,flashColor,flashToColorDuration);
            yield return new WaitForSeconds(flashToColorDuration);
            LeanTween.color(sr.gameObject,original,flashToOriginalDuration);
            yield return new WaitForSeconds(flashToOriginalDuration);
        }
        sr.color = original;
    }
    public bool CheckIfWon() { return rules.FindWin(); }
    public void OnRestart()
    {
        if (!RestartReady) return;
        BeginRound();
    }
}
