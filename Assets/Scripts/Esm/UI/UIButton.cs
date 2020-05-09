using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(Button))]
public class UIButton : MonoBehaviour
{
	[SerializeField]
	private string path;

	[SerializeField]
	private string overPath;

	[SerializeField]
	private string pressedPath;

	private void Start()
	{
		gameObject.name = path;
		var texture = BsaFileReader.LoadTexture(path) as Texture2D;
		var image = GetComponent<Image>();
		var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
		image.sprite = sprite;

		var button = GetComponent<Button>();
		var overTexture = BsaFileReader.LoadTexture(overPath) as Texture2D;
		var overSprite = Sprite.Create(overTexture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

		var pressedTexture = BsaFileReader.LoadTexture(pressedPath) as Texture2D;
		var pressedSprite = Sprite.Create(pressedTexture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

		var spriteState = button.spriteState;
		spriteState.highlightedSprite = overSprite;
		spriteState.pressedSprite = pressedSprite;

		button.spriteState = spriteState;
	}
}