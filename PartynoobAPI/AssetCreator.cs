using BepInEx.Logging;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
namespace PartynoobAPI
{
    static public class AssetCreator
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("PartynoobAPI");
        private static readonly Dictionary<object, object> _objectMap = new Dictionary<object, object>();
        private static AssetManager assetMan;
        public static LevelObject CloneLevelObjectDeepReflection(LevelObject originalLevelObject)
        {
            if (originalLevelObject == null)
            {
                return null;
            }

            _objectMap.Clear();
            return (LevelObject)InternalDeepCopy(originalLevelObject);
        }

        private static object InternalDeepCopy(object originalObject)
        {
            if (originalObject == null)
            {
                return null;
            }

            Type type = originalObject.GetType();
            if (type.IsValueType || type == typeof(string))
            {
                return originalObject;
            }

            if (_objectMap.ContainsKey(originalObject))
            {
                return _objectMap[originalObject];
            }

            if (originalObject is Object unityObject)
            {
                return Object.Instantiate(unityObject);
            }

            object newObject;
            try
            {
                newObject = Activator.CreateInstance(type);
            }
            catch (MissingMethodException)
            {
                Logger.LogError($"[ModName] Type '{type.FullName}' does not have a public parameterless constructor. " +
                                "Cannot create a new instance for deep copy. Returning original object (shallow copy).");
                return originalObject;
            }

            _objectMap.Add(originalObject, newObject);

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    object originalValue = field.GetValue(originalObject);

                    if (originalValue == null)
                    {
                        field.SetValue(newObject, null);
                        continue;
                    }

                    Type fieldType = field.FieldType;

                    if (typeof(IList).IsAssignableFrom(fieldType) && fieldType.IsGenericType)
                    {
                        IList originalList = (IList)originalValue;
                        IList newList = (IList)Activator.CreateInstance(fieldType);

                        foreach (object item in originalList)
                        {
                            newList.Add(InternalDeepCopy(item));
                        }
                        field.SetValue(newObject, newList);
                    }
                    else if (fieldType.IsArray)
                    {
                        Array originalArray = (Array)originalValue;
                        Array newArray = (Array)Activator.CreateInstance(fieldType, originalArray.Length);

                        for (int i = 0; i < originalArray.Length; i++)
                        {
                            newArray.SetValue(InternalDeepCopy(originalArray.GetValue(i)), i);
                        }
                        field.SetValue(newObject, newArray);
                    }
                    else if (!fieldType.IsValueType && fieldType != typeof(string) && !(originalValue is Object))
                    {
                        field.SetValue(newObject, InternalDeepCopy(originalValue));
                    }
                    else
                    {
                        field.SetValue(newObject, originalValue);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[ModName] Error copying field '{field.Name}' of type '{type.Name}': {ex.Message}");
                }
            }

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    try
                    {
                        object originalValue = prop.GetValue(originalObject, null);

                        if (originalValue == null)
                        {
                            prop.SetValue(newObject, null, null);
                            continue;
                        }

                        Type propType = prop.PropertyType;

                        if (typeof(IList).IsAssignableFrom(propType) && propType.IsGenericType)
                        {
                            IList originalList = (IList)originalValue;
                            IList newList = (IList)Activator.CreateInstance(propType);

                            foreach (object item in originalList)
                            {
                                newList.Add(InternalDeepCopy(item));
                            }
                            prop.SetValue(newObject, newList, null);
                        }
                        else if (propType.IsArray)
                        {
                            Array originalArray = (Array)originalValue;
                            Array newArray = (Array)Activator.CreateInstance(propType, originalArray.Length);

                            for (int i = 0; i < originalArray.Length; i++)
                            {
                                newArray.SetValue(InternalDeepCopy(originalArray.GetValue(i)), i);
                            }
                            prop.SetValue(newObject, newArray, null);
                        }
                        else if (!propType.IsValueType && propType != typeof(string) && !(originalValue is Object))
                        {
                            prop.SetValue(newObject, InternalDeepCopy(originalValue), null);
                        }
                        else
                        {
                            prop.SetValue(newObject, originalValue, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[ModName] Error copying property '{prop.Name}' of type '{type.Name}': {ex.Message}");
                    }
                }
            }

            return newObject;
        }

        /// <summary>
        /// Duplicates a sceneObject
        /// </summary>
        /// <param name="SO">The scene object to duplicate.</param>
        /// <param name="NewLvName">The new Level name (like F1,F2..)</param>
        /// <param name="LevelNo">the level Number, if you were to add F6 you'd put 5</param>
        /// <param name="SetPrevLevelToNewLevel">Sets the next level of the original sceneObject to this the new sceneObject</param>
        /// <param name="Isfinal">Sets it as a final level, useful if you dont want the pitstop for some reason</param>
        /// <returns>The cloned scene object</returns>
        public static SceneObject DuplicateAndNewLevel(
            this SceneObject SO,
            string NewLvName,
            int LevelNo,
            bool SetPrevLevelToNewLevel,
            bool Isfinal)
        {
            SceneObject sceneObj = SO;

            if (sceneObj != null)
            {
                SceneObject sceneObject = Object.Instantiate<SceneObject>(sceneObj);

                WeightedLevelObject[] weightedLevelObjectArray = new WeightedLevelObject[0];

                foreach (WeightedLevelObject originalWeightedLevelObject in sceneObj.randomizedLevelObject)
                {
                    WeightedLevelObject copyOfWeightedLevelObject = new WeightedLevelObject();

                    LevelObject copyOfLevelObject = null;
                    try
                    {
                        copyOfLevelObject = CloneLevelObjectDeepReflection(originalWeightedLevelObject.selection);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModName] Critical error during LevelObject deep reflection clone! " +
                                       $"Falling back to shallow copy of LevelObject. Error: {ex.Message}");
                        copyOfLevelObject = originalWeightedLevelObject.selection;
                    }

                    copyOfWeightedLevelObject.selection = copyOfLevelObject;
                    copyOfWeightedLevelObject.weight = originalWeightedLevelObject.weight;

                    weightedLevelObjectArray = HarmonyLib.CollectionExtensions.AddToArray<WeightedLevelObject>(weightedLevelObjectArray, copyOfWeightedLevelObject);
                }

                sceneObject.randomizedLevelObject = weightedLevelObjectArray;

                sceneObject.levelTitle = NewLvName;
                sceneObject.levelNo = LevelNo;
                sceneObject.nameKey = "MainLevel_" + LevelNo;
                sceneObject.name = "MainLevel_" + LevelNo;

                if (SetPrevLevelToNewLevel)
                {
                    sceneObj.nextLevel = sceneObject;
                }

                foreach (CustomLevelObject customLevelObject in sceneObject.GetCustomLevelObjects())
                {
                    LevelObject levelobj = Object.Instantiate<LevelObject>(customLevelObject);

                    if (Isfinal)
                    {
                        levelobj.finalLevel = true;
                        customLevelObject.finalLevel = true;
                    }
                    else
                    {
                        levelobj.finalLevel = false;
                        customLevelObject.finalLevel = false;
                    }

                    BaseGameManager gameman = sceneObject.manager;
                    LevelGenerationParameters genparam = gameman.levelObject;

                    genparam.name = "MainLevel_" + LevelNo;

                    gameman.name = "Lvl" + LevelNo + "_MainGameManager";
                    gameman.levelObject = genparam;
                    sceneObject.manager = gameman;

                    levelobj.name = levelobj.type + "_Lvl" + LevelNo;

                    sceneObject.levelObject = levelobj;
                }

                Logger.LogInfo($"<color=green>{NewLvName}</color> has been created with its previous level being {(sceneObj.levelTitle)}");
                return sceneObject;
            }

            Logger.LogError($"Level {NewLvName} Could not have been created because the Previous Level Has not been assigned");
            return null;
        }


        /// <summary>
        /// Gets a sprite from a URL (it needs to return an image otherwise might not work)
        /// </summary>
        /// <param name="url">The website that returns an image</param>
        /// <param name="center">The center of the sprite</param>
        /// <param name="pixelsperUnit">Pixels per unit</param>
        /// <returns>A Task<Sprite> to be able to get the actual sprite you must wait for it to finish, otherwise will crash or not load</returns>
        async public static Task<Sprite> GetSpriteFromURL(string url,Vector2 center, float pixelsperUnit = 1f)
        {
            byte[] asset;
            using (var httprequest = new HttpClient())
            {
                asset = await httprequest.GetByteArrayAsync(url);
                
                
            }

            return TextureToSprite(LoadTexture(asset, TextureFormat.RGBA32), Vector2.one, pixelsperUnit);
        }
        
        /// <summary>
        /// Creates a new sprite from the specified texture, using the given center point and pixels per unit.
        /// </summary>
        /// <param name="tex">The texture to use for the sprite. Cannot be null.</param>
        /// <param name="center">The normalized center point of the sprite's pivot, where (0,0) is the bottom-left and (1,1) is the
        /// top-right.</param>
        /// <param name="pixelsperunit">The number of pixels in the sprite that correspond to one unit in world space. Must be greater than zero.</param>
        /// <returns>A new sprite created from the specified texture, centered at the given pivot point and scaled according to
        /// the specified pixels per unit.</returns>
        public static Sprite TextureToSprite(Texture2D tex, Vector2 center, float pixelsperunit)
        {
            Sprite spr = Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),center, pixelsperunit,0u,SpriteMeshType.FullRect);
            return spr;
        }
        /// <summary>
        /// Creates a new Texture2D object from the specified image data and texture format.
        /// </summary>
        /// <remarks>The created texture uses point filtering and does not generate mipmaps. The input
        /// image data must be valid and compatible with the specified format, or the method may fail to load the image
        /// correctly.</remarks>
        /// <param name="bytes">A byte array containing the encoded image data to load into the texture. The data must be in a supported
        /// image format such as PNG or JPG.</param>
        /// <param name="format">The texture format to use when creating the Texture2D object.</param>
        /// <returns>A Texture2D instance containing the loaded image data.</returns>
        public static Texture2D LoadTexture(byte[] bytes, TextureFormat format)
        {
            Texture2D tex = new(2, 2, format, mipChain: false);
            tex.LoadImage(bytes);
            tex.filterMode = FilterMode.Point;
            return tex;

        }

    }


}
