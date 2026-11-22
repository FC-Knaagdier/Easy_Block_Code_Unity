using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[Serializable]
public class BlocklyTextPart
{
    public string text;
    public string variable;
}

[Serializable]
public class BlocklyCommand
{
    public string action;
    public float value;
    public string color;
    public string text;
    public string target;
    public string textVariable;
    public string variable;
    public string valueVariable;
    public BlocklyTextPart[] textParts;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class BlocklyProgram
{
    public BlocklyCommand[] commands;
    public BlocklyCommand[] updateCommands;
}

public class BlocklyInterpreter : MonoBehaviour
{
    public float moveSpeed = 1f;
    public float turnSpeed = 1f;
    private Transform followTarget;
    private GameObject movementTarget;
    private float followSpeed;
    private readonly Dictionary<string, string> textVariables = new Dictionary<string, string>();
    private BlocklyCommand[] updateCommands = Array.Empty<BlocklyCommand>();

    public void SetMovementTarget(GameObject target)
    {
        movementTarget = target;
    }

    private void Update()
    {
        foreach (var command in updateCommands)
            ExecuteCommand(command);

        if (followTarget != null)
            transform.position = Vector3.MoveTowards(transform.position, followTarget.position, followSpeed * Time.deltaTime);
    }

    public void HandleMessage(string json)
    {
        try
        {
            var program = JsonUtility.FromJson<BlocklyProgram>(json);
            if (program == null || program.commands == null)
            {
                Debug.LogWarning("[BlocklyInterpreter] Ongeldige JSON: " + json);
                return;
            }

            updateCommands = program.updateCommands ?? Array.Empty<BlocklyCommand>();
            textVariables.Clear();

            foreach (var command in program.commands)
            {
                ExecuteCommand(command);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[BlocklyInterpreter] Fout bij parsen: " + ex.Message);
        }
    }

    private void ExecuteCommand(BlocklyCommand command)
    {
        switch (command.action)
        {
            case "move_forward":
                transform.position += transform.forward * command.value * moveSpeed;
                break;
            case "move_backward":
                transform.position -= transform.forward * command.value * moveSpeed;
                break;
            case "move_up":
                transform.position += Vector3.up * command.value * moveSpeed;
                break;
            case "move_down":
                transform.position -= Vector3.up * command.value * moveSpeed;
                break;
            case "set_position":
                transform.position = new Vector3(command.x, command.y, command.z);
                break;
            case "turn_right":
                transform.Rotate(Vector3.up, command.value * turnSpeed);
                break;
            case "turn_left":
                transform.Rotate(Vector3.up, -command.value * turnSpeed);
                break;
            case "set_scale":
                transform.localScale = Vector3.one * command.value;
                break;
            case "set_color":
                if (!ColorUtility.TryParseHtmlString(command.color, out var parsedColor))
                {
                    Debug.LogWarning("[BlocklyInterpreter] Ongeldige kleur: " + command.color);
                    break;
                }
                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                    spriteRenderer.color = parsedColor;
                else
                {
                    var renderer = GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.material.color = parsedColor;
                }
                break;
            case "say":
                Debug.Log(command.text);
                break;
            case "destroy_by_tag":
                try
                {
                    foreach (var tagged in GameObject.FindGameObjectsWithTag(command.text))
                    {
                        if (tagged.GetComponentInChildren<TMP_Text>(true) != null) continue;
                        Destroy(tagged);
                    }
                }
                catch (UnityException)
                {
                    Debug.LogWarning("[BlocklyInterpreter] Onbekende tag: " + command.text);
                }
                break;
            case "set_variable":
                textVariables[command.variable] = ResolveText(command.textParts, command.text, command.valueVariable);
                break;
            case "set_text":
                var textObject = GameObject.Find(command.target);
                var textField = textObject != null ? textObject.GetComponentInChildren<TMP_Text>(true) : null;
                if (textField == null)
                    Debug.LogWarning("[BlocklyInterpreter] Geen TextMesh Pro-veld gevonden op object: " + command.target);
                else
                    textField.text = ResolveText(command.textParts, command.text, command.textVariable);
                break;
            case "follow_object":
            case "move_towards_object":
                var target = movementTarget != null ? movementTarget : GameObject.Find(command.text);
                if (target == null)
                {
                    Debug.LogWarning("[BlocklyInterpreter] Stel Movement Target in bij BlocklyIntegration of controleer de doelnaam: " + command.text);
                    break;
                }

                followTarget = target.transform;
                followSpeed = command.value;
                break;
            default:
                Debug.LogWarning("[BlocklyInterpreter] Onbekend commando: " + command.action);
                break;
        }
    }

    private string ResolveText(BlocklyTextPart[] parts, string text, string variable)
    {
        if (parts != null)
        {
            var builder = new StringBuilder();
            foreach (var part in parts)
            {
                if (part == null)
                    continue;

                if (!string.IsNullOrEmpty(part.variable))
                {
                    if (textVariables.TryGetValue(part.variable, out var partValue))
                        builder.Append(partValue);
                }
                else
                {
                    builder.Append(part.text);
                }
            }

            return builder.ToString();
        }

        if (!string.IsNullOrEmpty(variable))
            return textVariables.TryGetValue(variable, out var value) ? value : string.Empty;
        return text ?? string.Empty;
    }
}
