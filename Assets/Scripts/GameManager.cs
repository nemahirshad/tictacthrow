using System.Collections;
using System.Collections.Generic;
using TouchScript.Examples.Tap;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Game Elements")]
    [SerializeField] GameObject board;
    [SerializeField] GameObject endGameScreen;
    [SerializeField] Text text;

    [Header("Board Lines Animation (LeanTween)")]
    [SerializeField] List<GameObject> horizontalBoardLines;
    [SerializeField] List<GameObject> verticalBoardLines;
    [SerializeField] float lineAnimationDuration = 0.4f;
    [SerializeField] float animationDelayBetweenLines = 0.1f;
    [SerializeField] Vector3 targetHorizontalLineScale = Vector3.one;
    [SerializeField] Vector3 targetVerticalLineScale = Vector3.one;
    [SerializeField] LeanTweenType easeType = LeanTweenType.easeOutExpo;

    [Header("Symbol Animations (LeanTween)")]
    [SerializeField] float popInAnimationDuration = 0.4f;
    [SerializeField] LeanTweenType popInEaseType = LeanTweenType.easeOutBack;

    // --- Flip parameters
    [SerializeField] float flipDurationPart1 = 0.2f;
    [SerializeField] float flipDurationPart2 = 0.2f;
    [SerializeField] LeanTweenType flipEase = LeanTweenType.easeInOutSine;

    [SerializeField] Vector3 symbolTargetScale = Vector3.one;

    [Header("Gameplay Sprites & Logic")]
    [SerializeField] Sprite xSprite;
    [SerializeField] Sprite oSprite;
    [SerializeField] List<TileHandler> tileHandlers = new List<TileHandler>();

    [Header("Winning Symbol Flash Animation")]
    [SerializeField] Color flashColor = new Color(1f, 1f, 0.5f, 1f); 
    [SerializeField] float flashToColorDuration = 0.1f; 
    [SerializeField] float flashToOriginalDuration = 0.15f;
    [SerializeField] int numberOfFlashes = 3;  
    [SerializeField] float delayBeforeEndScreen = 0.5f;

    [SerializeField] TextMeshProUGUI turnIndicatorText;

    bool isXTurn = true;
    [HideInInspector] public bool canPlay = false;
    int[] tiles = new int[9];
    int playCount;

    private List<int> winningTileIndices = new List<int>();

    public delegate void OnTileHit(SpriteRenderer spriteRenderer, int tileNum);
    public static OnTileHit tileHit;
    public static GameManager instance;

    private int totalLineTweensExpected = 0;
    private int lineTweensCompleted = 0;

    private void Start()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        tileHit += HandleTileHit;

        if (board != null) board.SetActive(true); 
        if (endGameScreen != null) endGameScreen.SetActive(false);

        AnimateBoardLinesEntry();
    }


    void AnimateBoardLinesEntry()
    {
        InitializeGame(); 

        canPlay = false;

        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = "";
        }

        lineTweensCompleted = 0;
        totalLineTweensExpected = 0;

        if (horizontalBoardLines != null)
        {
            foreach (GameObject line in horizontalBoardLines) { if (line != null) totalLineTweensExpected++; }
        }
        if (verticalBoardLines != null)
        {
            foreach (GameObject line in verticalBoardLines) { if (line != null) totalLineTweensExpected++; }
        }

        if (totalLineTweensExpected == 0)
        {
            Debug.LogWarning("GameManager: No board lines assigned for animation. Starting game immediately.");
            canPlay = true;
            return;
        }

        float currentDelay = 0f;

        // Animate Horizontal Lines
        if (horizontalBoardLines != null)
        {
            foreach (GameObject line in horizontalBoardLines)
            {
                if (line != null)
                {
                    line.SetActive(true);
                    line.transform.localScale = new Vector3(0f, targetHorizontalLineScale.y, targetHorizontalLineScale.z);
                    LeanTween.scaleX(line, targetHorizontalLineScale.x, lineAnimationDuration)
                        .setEase(easeType)
                        .setDelay(currentDelay)
                        .setOnComplete(OnSingleLineAnimationComplete);
                    currentDelay += animationDelayBetweenLines;
                }
            }
        }

        // Animate Vertical Lines
        if (verticalBoardLines != null)
        {
            foreach (GameObject line in verticalBoardLines)
            {
                if (line != null)
                {
                    line.SetActive(true);
                    line.transform.localScale = new Vector3(targetVerticalLineScale.x, 0f, targetVerticalLineScale.z);
                    LeanTween.scaleY(line, targetVerticalLineScale.y, lineAnimationDuration)
                        .setEase(easeType)
                        .setDelay(currentDelay)
                        .setOnComplete(OnSingleLineAnimationComplete);
                    currentDelay += animationDelayBetweenLines;
                }
            }
        }
    }


    void OnSingleLineAnimationComplete()
    {
        lineTweensCompleted++;
        if (lineTweensCompleted >= totalLineTweensExpected)
        {
            InitializeGameAndAllowPlay();
        }
    }

    void AnimatePopIn(GameObject symbolGO)
    {
        symbolGO.SetActive(true);
        symbolGO.transform.localScale = Vector3.zero;

        LeanTween.scale(symbolGO, symbolTargetScale, popInAnimationDuration)
            .setEase(popInEaseType);
    }

    void AnimateFlip(GameObject symbolGO, Sprite newSpriteToShow)
    {
        symbolGO.SetActive(true);
        symbolGO.transform.localScale = symbolTargetScale;

        LeanTween.scaleY(symbolGO, 0f, flipDurationPart1)
            .setEase(flipEase)
            .setOnComplete(() => 
            {
                SpriteRenderer sr = symbolGO.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = newSpriteToShow;
                }

                LeanTween.scaleY(symbolGO, symbolTargetScale.y, flipDurationPart2)
                    .setEase(flipEase);
            });
    }
    void InitializeGameAndAllowPlay()
    {
        Debug.Log("Board lines animation complete. Allowing play.");
        canPlay = true;
        UpdateTurnIndicatorText();
    }

    void InitializeGame()
    {
        for (int i = 0; i < 9; i++) { tiles[i] = i + 9; }
        playCount = 0;
        isXTurn = true;
        if (tileHandlers != null)
        {
            foreach (TileHandler th in tileHandlers) { if (th != null) th.CleanUp(); }
        }
        //if (board != null) board.SetActive(true);
        //if (endGameScreen != null) endGameScreen.SetActive(false);
    }

    void AnimateSymbolFlash(SpriteRenderer symbolSpriteRenderer)
    {
        if (symbolSpriteRenderer == null || symbolSpriteRenderer.sprite == null)
        {
            Debug.LogWarning("Attempted to flash a null sprite or SpriteRenderer.");
            return;
        }

        Color originalColor = symbolSpriteRenderer.color;

        LTSeq sequence = LeanTween.sequence();
        for (int i = 0; i < numberOfFlashes; i++)
        {
            sequence.append(LeanTween.color(symbolSpriteRenderer.gameObject, flashColor, flashToColorDuration));
            sequence.append(LeanTween.color(symbolSpriteRenderer.gameObject, originalColor, flashToOriginalDuration));
        }
    }
 

    void HandleTileHit(SpriteRenderer tileSpriteRenderer, int tileNum)
    {
        if (!canPlay || tileNum < 0 || tileNum >= tiles.Length) return;

        int previousTileState = tiles[tileNum];
        int currentPlayerMark = isXTurn ? 1 : 0;
        bool shouldToggleTurn;
        GameObject symbolGameObject = tileSpriteRenderer.gameObject;

        if (previousTileState >= 9)
        {
            playCount++;
            shouldToggleTurn = true;
            tiles[tileNum] = currentPlayerMark;
            tileSpriteRenderer.sprite = isXTurn ? xSprite : oSprite;
            AnimatePopIn(symbolGameObject);
        }
        else
        {
            if (previousTileState == currentPlayerMark)
            {
                shouldToggleTurn = false;
            }
            else
            {
                shouldToggleTurn = true;
                tiles[tileNum] = currentPlayerMark;
                Sprite newSpriteForFlip = isXTurn ? xSprite : oSprite;
                AnimateFlip(symbolGameObject, newSpriteForFlip);
            }
        }

        if (CheckIfWon())
        {
            canPlay = false;
            string winner = isXTurn ? "X" : "O";
            Debug.Log(winner + " wins!");

            if (turnIndicatorText != null)
            {
                turnIndicatorText.text = $"{winner} Wins!";
            }

            foreach (int index in winningTileIndices)
            {
                if (index >= 0 && index < tileHandlers.Count && tileHandlers[index] != null)
                {
                    SpriteRenderer srToFlash = tileHandlers[index].GetSymbolSpriteRenderer();
                    if (srToFlash != null && srToFlash.sprite != null)
                    {
                        AnimateSymbolFlash(srToFlash);
                    }
                }
            }

            float totalFlashAnimationTime = numberOfFlashes * (flashToColorDuration + flashToOriginalDuration);
            StartCoroutine(DelayedStartOver(totalFlashAnimationTime + delayBeforeEndScreen, $"{winner} Wins!"));
        }
        else if (playCount >= 9)
        {
            canPlay = false;
            Debug.Log("Tie");

            if (turnIndicatorText != null)
            {
                turnIndicatorText.text = "It's a Tie!";
            }

            StartCoroutine(StartOver("Tie")); 
        }
        else 
        {
            if (shouldToggleTurn)
            {
                isXTurn = !isXTurn;
                UpdateTurnIndicatorText(); 
            }
        }
    }

    public bool CheckIfWon()
    {
        winningTileIndices.Clear();

        int[,] lines = new int[,]
        {
            {0,1,2}, {3,4,5}, {6,7,8},
            {0,3,6}, {1,4,7}, {2,5,8},
            {0,4,8}, {2,4,6}           
        };

        for (int i = 0; i < lines.GetLength(0); i++)
        {
            int a = lines[i, 0];
            int b = lines[i, 1];
            int c = lines[i, 2];

            if (tiles[a] <= 1 && tiles[a] == tiles[b] && tiles[a] == tiles[c])
            {
                winningTileIndices.Add(a);
                winningTileIndices.Add(b);
                winningTileIndices.Add(c);
                return true;
            }
        }
        return false;
    }

    IEnumerator DelayedStartOver(float delay, string message)
    {
        yield return new WaitForSeconds(delay);
        StartCoroutine(StartOver(message));
    }

    IEnumerator StartOver(string winMessage)
    {
        canPlay = false;
        yield return new WaitForSeconds(0.5f);
        if (board != null) board.SetActive(false);
        if (endGameScreen != null) endGameScreen.SetActive(true);
        if (text != null) text.text = winMessage;
    }
    public void OnRestart()
    {
        Debug.Log("Restarting game...");

        if (board != null) board.SetActive(true);
        if (endGameScreen != null) endGameScreen.SetActive(false);

        AnimateBoardLinesEntry();
    }

    void UpdateTurnIndicatorText()
    {
        if (turnIndicatorText == null) return;

        if (canPlay)
        {
            if (isXTurn)
            {
                turnIndicatorText.text = "Player X's Turn";
            }
            else
            {
                turnIndicatorText.text = "Player O's Turn";
            }
        }
    }
}