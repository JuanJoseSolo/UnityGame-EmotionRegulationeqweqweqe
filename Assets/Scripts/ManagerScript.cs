using ActionLibrary;
using EmotionalAppraisal;
using EmotionalAppraisal.DTOs;
using EmotionalAppraisal.OCCModel;
using GAIPS.Rage;
using IntegratedAuthoringTool;
using IntegratedAuthoringTool.DTOs;
using RolePlayCharacter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using WellFormedNames;
using WorldModel;
// Emotion Regulation .dll or scripts 
using EmotionRegulation.Components;
using EmotionRegulation.BigFiveModel;


public class ManagerScript : MonoBehaviour
{
    // Store the iat file
    private IntegratedAuthoringToolAsset _iat;

    [Header("Folder and File Names")]
    public string rootFolder;
    public string scenarioName;
    public string storageName;

    //Store the characters
    private List<RolePlayCharacterAsset> _rpcList;
    private AssetStorage storage;

    //Store the World Model
    private WorldModelAsset _worldModel;

    [Header("Prefabs")]
    public Button DialogueButtonPrefab;
    private RolePlayCharacterAsset _playerRpc;
    private bool _waitingForPlayer = false;
    private List<Button> _mButtonList = new List<Button>();
    private List<UnityBodyImplement> _agentBodyControlers;

    // We need to save information returned by UnityWebRequest during the loading process
    string scenarioInfo = "";
    string storageInfo = "";
    bool scenarioDone = false;
    bool storageDone = false;

    //Dealing with Audio and XML relevant for Web-GL
    UnityWebRequest audio;
    UnityWebRequest xml;
    string initiator;
    bool audioReady = false;
    bool xmlReady = false;
    bool audioNeeded = false;

    //public Canvas initialCanvas;
    public Canvas GameCanvas;
    public Canvas ERconfCanvas;
    public Canvas EvtsAvoidCanvas;
    public Canvas ActionsForEvtsCanvas;
    public Canvas UseERCanvas;
    public Canvas SetPersonalityCanvas;
    public Canvas SubActions4Evts;
    public Canvas EvtsToReinterpretedCanvas;
    public Canvas EvtsReinterpretedCanvas;

    // Choose your character button prefab
    public Button menuButtonPrefab;

    // Auxiliary Variables
    private bool initialized = false;

    // Time given to each character's dialogue in case there is no text to speech
    public float dialogueTimer;

    // Auxiliary variable
    private float dialogueTimerAux;

    // If there is no text to speech leave at false
    public bool useTextToSpeech;

    // If agents need to get to know each other
    public bool introduceAgents;

    // Different models available to agents
    public List<GameObject> CharacterBodies;

    private bool isReady = false;
    private Dictionary<string, GameObject> nameToBody;

    // Emotion Regulation resources
    private bool ERready = false;

    // We can decide to use or not use the emotion regulation asset.
    private bool UseER;

    // set personalitly 
    private float Openness = 0,
         Conscientiousness = 0,
         Extraversion = 0,
         Agreeableness = 0,
         Neuroticism = 0,
         MaxLevelEmotion = 0;
    // Time to display the emotion regulation inforamation
    private float cont = 60;
    // The principal Emotion Regulation asset
    private RegulationBasedAgent agent;
    // Set character FAtiMA 
    private RolePlayCharacterAsset character;
    // To set the personality traits
    private PersonalityDTO personalityDTO;
    // Store the new decision by the Emotion Regulation asset
    IAction newDecision = null;
    // It store the events that the agent will be able to avoid
    private List<AppraisalRuleDTO> appRulesToEvtsAvoid = new List<AppraisalRuleDTO>();
    private List<AppraisalRuleDTO> appRulesToEvtsReinterpreted = new List<AppraisalRuleDTO>();
    // It store the actions that the agent will try to apply the second strategy
    List<ActionsforEvent> actionsForEvents = new List<ActionsforEvent>();
    List<ActionsforEvent> actionsForEvents2 = new List<ActionsforEvent>();
    List<AppraisalRuleDTO> listEvtsToReinterpreted = new List<AppraisalRuleDTO>();

    // Use this for initialization
    void Start()
    {
        Debug.Log("Loading...");
        var streamingAssetsPath = Application.streamingAssetsPath;
#if UNITY_EDITOR || UNITY_STANDALONE

        streamingAssetsPath = "file://" + streamingAssetsPath;
#endif
        nameToBody = new Dictionary<string, GameObject>();
        // Loading Storage json with the Rules, files must be in the Streaming Assets Folder
        var storagePath = streamingAssetsPath + "/" + rootFolder + "/" + storageName + ".json";

        // Loading Scenario information with data regarding characters and dialogue
        var iatPath = streamingAssetsPath + "/" + rootFolder + "/" + scenarioName + ".json";
        // turn off this canvas
        ERconfCanvas.gameObject.SetActive(false);
        GameCanvas.gameObject.SetActive(false);

        StartCoroutine(GetStorage(storagePath));
        StartCoroutine(GetScenario(iatPath));
    }
    void LoadedScenario()
    {
        var currentState = IATConsts.INITIAL_DIALOGUE_STATE;
        // Getting a list of all the Characters
        _rpcList = _iat.Characters.ToList();
        // Saving the World Model
        _worldModel = _iat.WorldModel;
        Debug.Log("Loading has finished");
        isReady = true;
        //ChooseCharacterMenu();
        UseEmotionRegulation(); // Initial canvas for Emotion Regulation game.
    }
    private void LoadGame(RolePlayCharacterAsset rpc)
    {
        _playerRpc = rpc;

        _playerRpc.IsPlayer = true;
        // Turn off the choose your character panel
        // initialCanvas.gameObject.SetActive(false);
        TurnOffCanvas();
        //Turning on the Dialogue canvas
        GameCanvas.gameObject.SetActive(true);

        // Update character's name in the Game although I'm overcomplicating things a bit.
        // Quita al character que eligió el usuario para no cargar los controles de Unity.
        var otherRPCsList = _rpcList;
        otherRPCsList.Remove(rpc);

        _agentBodyControlers = new List<UnityBodyImplement>();

        foreach (var agent in otherRPCsList)
        {
            // Initializing textual for each different character
            var charName = agent.CharacterName.ToString();
            var rand = UnityEngine.Random.Range(0, CharacterBodies.Count);
            nameToBody.Add(charName, CharacterBodies[rand]);
            CharacterBodies.RemoveAt(rand);
            var body = nameToBody[charName];
            //Initializing and saving into a list the Body Controller of the First Character
            var unityBodyImplement = body.GetComponent<UnityBodyImplement>();
            body.name = charName;
            body.GetComponentInChildren<TextMesh>().text = charName;

            _agentBodyControlers.Add(unityBodyImplement);
        }

        // Lo vuleve a agregar a la lista original de characteres.
        _rpcList.Add(_playerRpc);

        if (introduceAgents)
        {
            List<Name> events = new List<Name>();
            foreach (var r in _rpcList)
            {
                events.Add(EventHelper.ActionEnd(r.CharacterName, (Name)"Enters", (Name)"-"));
            }

            foreach (var r in _rpcList)
            {
                r.Perceive(events);
                r.Update();
            }
        }
        // Checking information about the characters on scene.
        Debug.Log("Character on scene: " + _playerRpc.CharacterName);
        var CharacterPlay = _playerRpc.CharacterName;
        var characterOnScene = _rpcList.FirstOrDefault(a => a.CharacterName != CharacterPlay);
        Debug.Log("Character to ER : " + characterOnScene.CharacterName);

        //-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        //  Initialization of Emotion Regulation Asset. We need to send the character that is in the scene as paremeter.
        //  Set manual manner.
        //-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

        //SetEmotionRegualtionData(_rpcList.Find(c => c.CharacterName != rpc.CharacterName));

        initialized = true;
    }
    // Initial canvas, now it isn't use
    void ChooseCharacterMenu()
    {
        EvtsAvoidCanvas.gameObject.SetActive(false);
        ActionsForEvtsCanvas.gameObject.SetActive(false);
        foreach (var rpc in _rpcList)
        {
            // What happens when the player chooses to be a particular character
            AddButton(rpc.CharacterName.ToString(), () =>
            {
                UseER = GameObject.Find("ToggleER").GetComponent<Toggle>();
                if (UseER)
                {
                    SetPersonalityTraits(rpc);
                    if (!ERready) return;
                }
                else LoadGame(rpc);
            });
        }
    }
    // This way allows us to have higher control over the canvas
    private void TurnOffCanvas()
    {
        EvtsAvoidCanvas.gameObject.SetActive(false);
        ERconfCanvas.gameObject.SetActive(false);
        UseERCanvas.gameObject.SetActive(false);
        ActionsForEvtsCanvas.gameObject.SetActive(false);
        SetPersonalityCanvas.gameObject.SetActive(false);
        SubActions4Evts.gameObject.SetActive(false);
        EvtsToReinterpretedCanvas.gameObject.SetActive(false);
        EvtsReinterpretedCanvas.gameObject.SetActive(false);
    }
    private void UseEmotionRegulation()
    {
        /// We can decide if we want to use the emotional regulation asset or not.
        TurnOffCanvas();
        UseERCanvas.gameObject.SetActive(true);

        var agent = _rpcList.Find(x => x.CharacterName == (Name)"Pedro"); //TODO: To improve this method.
        character = _rpcList.Find(y => y.CharacterName == (Name)"Player");

        Button btnUseER = GameObject.Find("btnUseER").GetComponent<Button>();
        btnUseER.onClick.AddListener(() => { EmotionRegulationConf(agent); UseER = true; if (!ERready) return; });
        Button btnNotUseER = GameObject.Find("btnNotUseER").GetComponent<Button>();
        btnNotUseER.onClick.AddListener(() => { LoadGame(character); });// The simulation shall start with the agent's player.
    }
    private void EmotionRegulationConf(RolePlayCharacterAsset agentRPC)
    {
        ERconfCanvas.gameObject.SetActive(true);
        agent = new RegulationBasedAgent { InputData = new InputData() }; /// To create an empty instance of the agent and input data.

        Button btnSetPersonality = GameObject.Find("btnSetPersonality").GetComponent<Button>();
        Button btnSetEvtsAvoid = GameObject.Find("btnSetEvtsAvoid").GetComponent<Button>();
        Button btnSetActionsForEvts = GameObject.Find("btnActionsForEvts").GetComponent<Button>();
        Button btnSetEvtsReinterpret = GameObject.Find("btnEvtsReinterpret").GetComponent<Button>();
        Button btnDone = GameObject.Find("btnDone").GetComponent<Button>();
        Button btnReturn = GameObject.Find("btnReturn").GetComponent<Button>();
        /// these buttons only we can use once we have configurated the personality of the agent, otherwise, the buttons are deactivated
        btnSetEvtsAvoid.enabled = false;
        btnSetActionsForEvts.enabled = false;
        btnSetEvtsReinterpret.enabled = false;
        btnDone.enabled = false;
        btnSetPersonality.onClick.AddListener(() =>
        {
            SetPersonalityTraits(agentRPC);
            btnDone.enabled =
            btnSetEvtsAvoid.enabled = true;
            btnSetEvtsReinterpret.enabled = true;
            btnSetActionsForEvts.enabled = true;
        });
        btnSetEvtsAvoid.onClick.AddListener(() => { EvtsAvoidCanvas.gameObject.SetActive(true); SetEventsToAvoid(agentRPC); });
        btnSetActionsForEvts.onClick.AddListener(() => { ActionsForEvtsCanvas.gameObject.SetActive(true); DisplayEventsForActions(); });
        btnSetEvtsReinterpret.onClick.AddListener(() => { EvtsToReinterpretedCanvas.gameObject.SetActive(true); SetEventToReinterpreted(); });
        btnDone.onClick.AddListener(() =>
        {
            ERready = true;
            InputData inputs = new InputData
            {
                IAT_FAtiMA = _iat,
                //appRulesToEvtsAvoid,
                ActionsForEvent = actionsForEvents,
                EventsToReappraisal = appRulesToEvtsReinterpreted
            };
            agent = new RegulationBasedAgent(agentRPC, personalityDTO, inputs);/// Generate the agent and all his information needed.
            EvtsAvoidCanvas.gameObject.SetActive(false);
            // Start normal game with the Emotional Regulation asset
            LoadGame(character);
        });
        btnReturn.onClick.AddListener(() => { UseEmotionRegulation(); });
    }
    private void SetPersonalityTraits(RolePlayCharacterAsset aget)
    {
        GameCanvas.gameObject.SetActive(false);
        ERconfCanvas.gameObject.SetActive(false);
        SetPersonalityCanvas.gameObject.SetActive(true);
        var conscientiousnessSlider = GameObject.Find("Slider_C").GetComponent<Slider>();
        var opennesSlider = GameObject.Find("Slider_O").GetComponent<Slider>();
        var maxVSlider = GameObject.Find("Slider_MaxV").GetComponent<Slider>();

        var btnComplete = GameObject.Find("btnCompletePersonality").GetComponent<Button>();
        btnComplete.onClick.AddListener(() =>
        {
            Openness = opennesSlider.value;
            MaxLevelEmotion = maxVSlider.value;
            Conscientiousness = conscientiousnessSlider.value;

            personalityDTO = new PersonalityDTO()
            {
                Openness = this.Openness,
                Conscientiousness = this.Conscientiousness,
                Extraversion = this.Extraversion,
                Agreeableness = this.Agreeableness,
                Neuroticism = this.Neuroticism,
                MaxLevelEmotion = this.MaxLevelEmotion
            };

            SetPersonalityCanvas.gameObject.SetActive(false);
            ERconfCanvas.gameObject.SetActive(true);
        });
    }
    private void SetEventsToAvoid(RolePlayCharacterAsset agent)
    {
        EvtsAvoidCanvas.gameObject.SetActive(true);
        var allEvents = agent.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();
        AddDialogueButtons(allEvents, appRulesToEvtsAvoid);

        var readybtn = GameObject.Find("btnCompleteAvoid").GetComponent<Button>();
        readybtn.onClick.AddListener(() =>
        {
            EvtsAvoidCanvas.gameObject.SetActive(false);
        });
    }
    private void DisplayEventsForActions()
    {
        var allEvents = character.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();
        AddDialogueButtons(allEvents);
        var readybtn = GameObject.Find("btnCompleteActions").GetComponent<Button>();
        readybtn.onClick.AddListener(() =>
        {
            ActionsForEvtsCanvas.gameObject.SetActive(false);
        });
    }
    private void SetActionsForEvts(Name evtName, AppraisalRuleDTO apprule, ActionsforEvent actions)
    {
        SubActions4Evts.gameObject.SetActive(true);
        ActionsForEvtsCanvas.gameObject.SetActive(false);
        GameObject.Find("CurrentEvt").GetComponent<Text>().text = evtName.ToString();
        var btnDoneSubActionsEvts = GameObject.Find("btnDoneSubActionsEvts").GetComponent<Button>();
        var btnAddAction = GameObject.Find("btnAddAction").GetComponent<Button>();
        var listActions = new List<KeyValuePair<string, float>>();
        actionsForEvents = new List<ActionsforEvent>();

        btnAddAction.onClick.AddListener(() =>
        {
            var actionName = GameObject.Find("InputAction").GetComponent<InputField>().text;
            var actionValue = GameObject.Find("InputValueAction").GetComponent<InputField>().text;
            if (actionName != "" && actionValue != "")
                listActions.Add(new KeyValuePair<string, float>(actionName, float.Parse(actionValue))); //TODO: add expection action 

            GameObject.Find("InputAction").GetComponent<InputField>().text = "";
            GameObject.Find("InputValueAction").GetComponent<InputField>().text = "";
        });

        btnDoneSubActionsEvts.onClick.AddListener(() =>
        {
            var isSpeak = evtName.GetFirstTerm() == (Name)"Speak";
            if (isSpeak)
            {
                actions.EventName = evtName.GetNTerm(4);
            }
            else
                actions.EventName = evtName;

            actions.AppraisalRulesOfEvent = new List<AppraisalRuleDTO> { apprule };/// Change this parameter into original architecture. Why did I decide to use a list for store the appraisal rules?
            actions.ActionNameValue = listActions;
            actionsForEvents.Add(actions);

            SubActions4Evts.gameObject.SetActive(false);
            ActionsForEvtsCanvas.gameObject.SetActive(true);
        });
    }
    Name nombreDelEvento; // open point: this variable is used to adding the name's event into the target of the event matching template, it have to removed
    private void SetEventToReinterpreted()
    {
        var listAppVar = new List<string>()
        {
            OCCAppraisalVariables.DESIRABILITY,
            OCCAppraisalVariables.LIKE,
            OCCAppraisalVariables.DESIRABILITY_FOR_OTHER,
            OCCAppraisalVariables.PRAISEWORTHINESS
        };
        TurnOffCanvas();
        EvtsToReinterpretedCanvas.gameObject.SetActive(true);
        EvtsReinterpretedCanvas.gameObject.SetActive(true);
        List<Dropdown.OptionData> items = new List<Dropdown.OptionData>();
        foreach (var appVar in listAppVar)
        {
            items.Add(new Dropdown.OptionData { text = appVar });
        }
        var appVars = GameObject.Find("InputAppVars").GetComponent<Dropdown>();
        appVars.options = items;
        var allEvents = character.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();
        List<AppraisalRuleDTO> app = new List<AppraisalRuleDTO>();
        var evtNameObj = GameObject.Find("EventName");

        AddDialogueButtons(allEvents, app, evtNameObj);

        var btnAddAppRule = GameObject.Find("btnAddAppRule").GetComponent<Button>();
        var btnDone = GameObject.Find("btnDone").GetComponent<Button>();
        var btnComplete = GameObject.Find("btnComplete").GetComponent<Button>();

        btnAddAppRule.onClick.AddListener(() =>
        {
            var vselected = appVars.captionText;
            bool isSpeak = nombreDelEvento.GetFirstTerm() == (Name)"Speak";
            if(isSpeak)            
                AddAppraisalRule(vselected.text, nombreDelEvento.GetNTerm(4).ToString());
            else AddAppraisalRule(vselected.text, nombreDelEvento.ToString());
            GameObject.Find("InputEvtName").GetComponent<InputField>().text = "";
            GameObject.Find("ValueAppVarppVar").GetComponent<InputField>().text = "";
        });

        btnDone.onClick.AddListener(() => 
        {
            EvtsReinterpretedCanvas.gameObject.SetActive(true);
        });

        btnComplete.onClick.AddListener(() => { EvtsToReinterpretedCanvas.gameObject.SetActive(false); ERconfCanvas.gameObject.SetActive(true); });

    }
    private void AddAppraisalRule(string appVariable, string targetName = "*")
    {
        EvtsToReinterpretedCanvas.gameObject.SetActive(true);

        var evtName = GameObject.Find("InputEvtName").GetComponent<InputField>();
        var appValue = GameObject.Find("ValueAppVarppVar").GetComponent<InputField>();

        string EMT = "Event(Action-End, *, GenericEventName, GenericTarget)";
        string newEvtName = evtName.text.Replace(' ', '_');
        var newEMT = EMT.Replace("GenericEventName", newEvtName);
        newEMT = newEMT.Replace("GenericTarget", targetName);


        if (!string.IsNullOrEmpty(newEMT) && !string.IsNullOrEmpty(appValue.text))
        {
            var appraisalVariableDTO = new List<AppraisalVariableDTO>()
            {
                new AppraisalVariableDTO() { Name = appVariable, Value = (Name)appValue.text }
            };
            var rule = new AppraisalRuleDTO()
            {
                EventMatchingTemplate = (Name)newEMT,
                AppraisalVariables = new AppraisalVariables(appraisalVariableDTO)
            };
            listEvtsToReinterpreted.Add(rule);

            appRulesToEvtsReinterpreted = listEvtsToReinterpreted;

        }

        EvtsToReinterpretedCanvas.gameObject.SetActive(true);
        ERconfCanvas.gameObject.SetActive(false);
    }
    private void DrownItemSelect(Dropdown dropdown)
    {
        EvtsToReinterpretedCanvas.gameObject.SetActive(true);
        var index = dropdown.value;
        var appVariable = dropdown.options[index].text;
        var evtName = GameObject.Find("InputEvtName").GetComponent<InputField>();
        var appValue = GameObject.Find("ValueAppVarppVar").GetComponent<InputField>();

        string EMT = "Event(Action-End, *, GenericEventName, *)";
        string newEvtName = evtName.text.Replace(' ', '_');
        var newEMT = EMT.Replace("GenericEventName", newEvtName);
        
        if(!string.IsNullOrEmpty(newEMT) && !string.IsNullOrEmpty(appValue.text))
        {
            var appraisalVariableDTO = new List<AppraisalVariableDTO>()
            {
                new AppraisalVariableDTO() { Name = appVariable, Value = (Name)appValue.text }
            };
            var rule = new AppraisalRuleDTO()
            {
                EventMatchingTemplate = (Name)newEMT,
                AppraisalVariables = new AppraisalVariables(appraisalVariableDTO)
            };

            var listEvtsToReinterpreted = new List<AppraisalRuleDTO>() { rule };
            appRulesToEvtsReinterpreted = listEvtsToReinterpreted;

        }

        EvtsToReinterpretedCanvas.gameObject.SetActive(true);
        ERconfCanvas.gameObject.SetActive(false);
    }

    // Instantiating the chose your character buttons
    private void AddButton(string label, UnityAction action)
    {
        var parent = GameObject.Find("ChooseCharacter");

        var button = Instantiate(menuButtonPrefab, parent.transform);

        var buttonLabel = button.GetComponentInChildren<Text>();
        buttonLabel.text = label;

        button.onClick.AddListener(action);
    }
    // Instantiating the Dialog Button Prefab on the DialogueOptions object in the Canvas
    private void AddDialogueButtons(List<DialogueStateActionDTO> dialogs, Name target)
    {
        var i = 0;
        foreach (var d in dialogs)
        {
            var b = Instantiate(DialogueButtonPrefab);
            var t = b.transform;
            t.SetParent(GameObject.Find("DialogueOptions").transform, false);
            i++;
            b.GetComponentInChildren<Text>().text = i + ": " + d.Utterance;
            var id = d.Id;
            b.onClick.AddListener(() => Reply(id, _playerRpc.CharacterName, target));
            _mButtonList.Add(b);
        }
    }
    private void AddDialogueButtons(List<AppraisalRuleDTO> currentsAppRules, List<AppraisalRuleDTO> storangeApprules, GameObject objGame = null)
    {
        foreach (var d in currentsAppRules)
        {
            var b = Instantiate(DialogueButtonPrefab);
            var t = b.transform;
            var objEvents = GameObject.Find("Events");
            t.SetParent(objEvents.transform, false);

            b.GetComponentInChildren<Text>().text = d.EventMatchingTemplate.GetNTerm(3).ToString();

            var id = d.Id;
            b.onClick.AddListener(() =>
            {
                nombreDelEvento = d.EventMatchingTemplate.GetNTerm(3);
                if (!(objGame is null))
                    objGame.GetComponent<Text>().text = nombreDelEvento.ToString();
                storangeApprules.Add(d); currentsAppRules.Remove(d); ClearAllDialogButtons(); AddDialogueButtons(currentsAppRules, storangeApprules);
                EvtsReinterpretedCanvas.gameObject.SetActive(false);
            });
            
            _mButtonList.Add(b);
        }
    }
    private void AddDialogueButtons(List<AppraisalRuleDTO> currentsAppRules)
    {
        foreach (var d in currentsAppRules)
        {
            var b = Instantiate(DialogueButtonPrefab);
            var t = b.transform;
            t.SetParent(GameObject.Find("Actions4Evts").transform, false);

            b.GetComponentInChildren<Text>().text = d.EventMatchingTemplate.GetNTerm(3).ToString();
            b.onClick.AddListener(() =>
            {
                var actionForEvent = new ActionsforEvent();
                currentsAppRules.Remove(d); ClearAllDialogButtons(); AddDialogueButtons(currentsAppRules);
                SetActionsForEvts(d.EventMatchingTemplate.GetNTerm(3), d, actionForEvent);
            });
            SubActions4Evts.gameObject.SetActive(false);
            _mButtonList.Add(b);
        }
    }
    void ClearAllDialogButtons()
    {
        foreach (var b in _mButtonList)
        {
            Destroy(b.gameObject);
        }
        _mButtonList.Clear();
    }

    //-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+--+-+-+-+-+--+
    //                          UPDATE
    //-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-

    // Update is called once per frame
    void Update()
    {
        if (!isReady)
        {
            if (scenarioDone && storageDone)
            {
                Debug.Log("Finished Reading Files");
                isReady = true;
                LoadWebGL();
            }
        }

        if (!initialized) return;
        if (!ERready && UseER) return;
        if (_agentBodyControlers.Any(x => x._speechController.IsPlaying))
            return;

        // Display the Emotional Regulation information 
        cont -= Time.deltaTime * 100;
        var Pedro = _rpcList.Find(x => x.CharacterName == (Name)"Pedro");
        var activeEmotion = Pedro.GetAllActiveEmotions();
        foreach(var emotion in activeEmotion)
        {
            GameObject.Find("EmotionsInfo").GetComponent<Text>().text = "Feels : " + emotion.Type + " = " + emotion.Intensity.ToString();
        }
        var dominant = agent?.DominantPersonality;
        var strategy = agent?.EmotionalRegulation?.StrategyApplied;

        GameObject.Find("Personality").GetComponent<Text>().text = "Dominant : " + dominant;
        GameObject.Find("Strategy").GetComponent<Text>().text = "Strategy : " + strategy;
        if (cont < 0)
        {
            cont = 60;
            _rpcList.FirstOrDefault().Update();
            var info = _rpcList.FirstOrDefault().CharacterName?.ToString();
            GameObject.Find("Info").GetComponent<Text>().text = info + "'s emotions";
            UpdateAgentFacialExpression();
        }

        if (audioNeeded)
        {
            if (audioReady && xmlReady)
                StartCoroutine(PlayAudio());
            else return;
        }

        if (_waitingForPlayer)
            return;
        if (dialogueTimerAux > 0)
        {
            dialogueTimerAux -= Time.deltaTime;
            return;
        }

        IAction finalDecision = null;
        String initiatorAgent = "";
        newDecision = null;
        
        // A simple cycle to go through all the agents and get their decision (for now there is only the Player and Charlie)
        foreach (var rpc in _rpcList)
        {
            // From all the decisions the rpc wants to perform we want the first one (as it is ordered by priority)
            var decision = rpc.Decide().FirstOrDefault();
            Debug.Log($"possible desicion: {decision}");
            if ( (UseER) && (agent.AgentName == rpc.CharacterName)) { newDecision = agent.Regulate(decision); }

            if (!(newDecision is null)) { decision = newDecision; } ;
            if (_playerRpc.CharacterName == rpc.CharacterName)
            {
                HandlePlayerOptions(decision);
                continue; ;
            }

            if (decision != null)
            {
                initiatorAgent = rpc.CharacterName.ToString();
                finalDecision = decision;
                //Write the decision on the canvas
                GameObject.Find("DecisionText").GetComponent<Text>().text =
                    " " + initiatorAgent + " decided to " + decision.Name.ToString() + " towards " + decision.Target;
                break;
            }
        }

        if (finalDecision != null)
        {
            ChooseDialogue(finalDecision, (Name)initiatorAgent);
        }
        // We can update the Facial Expression each frame to keep believability
        UpdateAgentFacialExpression();
    }
    void UpdateAgentFacialExpression()
    {
        foreach (var agent in _agentBodyControlers)
        {
            var strongestEmotion = _rpcList.Find(x => x.CharacterName.ToString() == agent.gameObject.name).GetStrongestActiveEmotion();

            if (strongestEmotion != null)
            {
                try
                {
                    Debug.Log("Agent:" + agent + " Emotion: " + strongestEmotion);
                    agent.SetExpression(strongestEmotion.EmotionType, strongestEmotion.Intensity / 10);
                } catch (Exception e)
                {
                    //There's only 4 different emotions -> face expression translation.
                    Debug.Log("Exception Caught: " + e.Message);
                }
            }
        }
    }
    // Method to play the audio file of the specific dialogue, aka what makes the agent talk
    private IEnumerator Speak(System.Guid id, Name initiator, Name target)
    {
        // The player has no body, as a consequence there is no reason for him to speak
        if (_playerRpc.CharacterName == initiator)
            yield break;
        audioNeeded = true;
        xmlReady = false;
        audioReady = false;
        this.initiator = initiator.ToString();
        // What is the type of of Voice of the agent
        var voiceType = _rpcList.Find(x => x.CharacterName == initiator).VoiceName;
        // Each utterance has a unique Id so we can retrieve its audio file
        var utteranceID = _iat.GetDialogActionById(id).UtteranceId;
        // This path can be changed, for now it is the path we used in this project
        var textToSpeechPath = "/" + rootFolder + "/TTS/" + voiceType + "/" + utteranceID;
        var absolutePath = Application.streamingAssetsPath;

#if UNITY_EDITOR || UNITY_STANDALONE
        absolutePath = "file://" + absolutePath;
#endif
        // System tries to "download" the .wav file along with its xml configuration
        string audioUrl = absolutePath + textToSpeechPath + ".wav";
        string xmlUrl = absolutePath + textToSpeechPath + ".xml";

        StartCoroutine(GetXML(xmlUrl));
        StartCoroutine(GetAudioURL(audioUrl));
    }
    private IEnumerator PlayAudio()
    {
        Debug.Log("Playing Audio");
        if (useTextToSpeech)
        {
            var clip = DownloadHandlerAudioClip.GetContent(audio);
            // The Unity Body Implement script allows us to play sound clips
            var initiatorBodyController = _agentBodyControlers.Find(x => x.gameObject.name == initiator.ToString());
            Debug.Log("initiator:" + initiator.ToString());
            yield return initiatorBodyController.PlaySpeech(clip, xml.downloadHandler.text);

            clip.UnloadAudioData();
            audioNeeded = false;
        }
        else audioNeeded = false;
    }
    void LoadWebGL()
    {
        Debug.Log("Loading Web Gl Method");
        Debug.Log("Loading Storage string");
        storage = AssetStorage.FromJson(storageInfo);
        Debug.Log("Loading IAT string");
        _iat = IntegratedAuthoringToolAsset.FromJson(scenarioInfo, storage);

        Debug.Log("Finished Loading Web-GL");
        LoadedScenario();
    }
    void Reply(Guid id, Name initiator, Name target)
    {
        dialogueTimerAux = dialogueTimer;
        // Retrieving the chosen dialog object
        var dialog = _iat.GetDialogActionById(id);
        var utterance = dialog.Utterance;
        var meaning = dialog.Meaning;
        var style = dialog.Style;
        var nextState = dialog.NextState;
        var currentState = dialog.CurrentState;

        // Playing the audio of the dialogue line
        if (useTextToSpeech)
        {
            this.StartCoroutine(Speak(id, initiator, target));
        }

        //Writing the dialog on the canvas
        GameObject.Find("DialogueText").GetComponent<Text>().text =
            initiator + " perform:  '" + utterance + "' ->towards " + target;

        // Getting the full action Name
        var actualActionName = "Speak(" + currentState + ", " + nextState + ", " + meaning +
                               ", " + style + ")";

        //So we can generate its event
        var eventName = EventHelper.ActionEnd(initiator, (Name)actualActionName, target);

        ClearAllDialogButtons();
        //Inform each participating agent of what happened

        _rpcList.Find(x => x.CharacterName == initiator).Perceive(eventName);
        _rpcList.Find(x => x.CharacterName == target).Perceive(eventName);

        //Handle the consequences of their actions
        HandleEffects(eventName);
    }
    void HandleEffects(Name _event)
    {
        var consequences = _worldModel.Simulate(new Name[] { _event });

        // For each effect 
        foreach (var eff in consequences)
        {
            Debug.Log("Effect: " + eff.PropertyName + " " + eff.NewValue + " " + eff.ObserverAgent);

            // For each Role Play Character
            foreach (var rpc in _rpcList)
            {
                //If the "Observer" part of the effect corresponds to the name of the agent or if it is a universal symbol
                if (eff.ObserverAgent != rpc.CharacterName && eff.ObserverAgent != (Name)"*") continue;
                //Apply that consequence to the agent
                rpc.Perceive(EventHelper.PropertyChange(eff.PropertyName, eff.NewValue, rpc.CharacterName));
            }
        }
        _waitingForPlayer = false;
    }
    void ChooseDialogue(IAction action, Name initiator)
    {
        Debug.Log(" The agent " + initiator + " decided to perform " + action.Name + " towards " + action.Target);

        //                                          NTerm: 0     1     2     3     4
        // If it is a speaking action it is composed by Speak ( [ms], [ns] , [m}, [sty])
        var currentState = action.Name.GetNTerm(1);
        var nextState = action.Name.GetNTerm(2);
        var meaning = action.Name.GetNTerm(3);
        var style = action.Name.GetNTerm(4);

        // Returns a list of all the dialogues given the parameters but in this case we only want the first element
        var dialog = _iat.GetDialogueActions(currentState, nextState, meaning, style).FirstOrDefault();

        if (dialog != null)
            Reply(dialog.Id, initiator, action.Target);
    }
    void HandlePlayerOptions(IAction decision)
    {
        _waitingForPlayer = true;
        if (decision != null)
            if (decision.Key.ToString() == "Speak")
            {
                //                                          NTerm: 0     1     2     3     4
                // If it is a speaking action it is composed by Speak ( [ms], [ns] , [m}, [sty])
                var currentState = decision.Name.GetNTerm(1);
                var nextState = decision.Name.GetNTerm(2);
                var meaning = decision.Name.GetNTerm(3);
                var style = decision.Name.GetNTerm(4);

                // Returns a list of all the dialogues given the parameters
                var dialog = _iat.GetDialogueActions(currentState, (Name)"*", (Name)"*", (Name)"*");

                foreach (var d in dialog)
                {
                    d.Utterance = _playerRpc.ProcessWithBeliefs(d.Utterance);
                }

                AddDialogueButtons(dialog, decision.Target);
            }
            else Debug.LogWarning("Unknown action: " + decision.Key);
    }
    IEnumerator GetScenario(string path)
    {
        UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);
            // Or retrieve results as binary data
            byte[] results = www.downloadHandler.data;
            scenarioInfo = www.downloadHandler.text;
            Debug.Log("Loaded Scenario:" + scenarioInfo.ToString());
            scenarioDone = true;
        }
    }
    IEnumerator GetStorage(string path)
    {
        UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);
            // Or retrieve results as binary data
            byte[] results = www.downloadHandler.data;
            storageInfo = www.downloadHandler.text;
            Debug.Log("Loaded Storage:" + storageInfo.ToString());
            storageDone = true;
        }
    }
    IEnumerator GetAudioURL(string path)
    {
        audio = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV);
        yield return audio.SendWebRequest();
        if (audio.result != UnityWebRequest.Result.Success)
        {
            if (audio.result == UnityWebRequest.Result.DataProcessingError || audio.result == UnityWebRequest.Result.ConnectionError || audio.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log("Audio not found error: " + audio.error);
                audioReady = true;
                useTextToSpeech = false;
                yield return null;
            }
        }
        else
        {
            audioReady = true;
        }
    }
    IEnumerator GetXML(string path)
    {
        UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            if (www.result == UnityWebRequest.Result.DataProcessingError || www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log("XML not found error: " + www.error);
                xmlReady = true;
                useTextToSpeech = false;
                yield return null;
            }
        }
        else
        {
            xml = www;
            xmlReady = true;
        }
    }
}
