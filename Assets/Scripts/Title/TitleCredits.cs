﻿using UnityEngine;
using UnityEngine.UI;

public class TitleCredits : MonoBehaviour {
    private bool visible = false;
    public void ShowCredits() {
        visible = true;
    }

    public void HideCredits() {
        visible = false;
    }

    private void Update() {
        Color color = GetComponent<Image>().color;
        if (visible) {
            color.a = Mathf.Min(0.8f, color.a + Time.deltaTime);
        } else {
            color.a = Mathf.Max(0f, color.a - Time.deltaTime);
        }
        GetComponent<Image>().color = color;

        transform.Find("Close").gameObject.SetActive(color.a > 0.6f);

        if (!visible && color.a < 0.1f) {
            gameObject.SetActive(false);
        }
    }
}
