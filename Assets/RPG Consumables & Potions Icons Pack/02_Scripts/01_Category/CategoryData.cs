using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CategoryData", menuName = "RPG Icons/Category Data")]
public class CategoryData : ScriptableObject
{
    public string categoryName;
    public Sprite[] icons;
}