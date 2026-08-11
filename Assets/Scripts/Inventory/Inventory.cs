using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    public static Inventory instance;

    public GameObject hotbarObj;
    public GameObject inventorySlotParent;
    public GameObject container;
    public Transform chestUI;
    public GameObject craftingtableMenu;

    public TextMeshProUGUI pickupPromptText;
    public TextMeshProUGUI lookedAtItemNameText;

    public Image dragIcon;

    public float pickupRange = 3f;

    private Material originalMaterial;
    private Renderer lookedAtRenderer = null;

    private int equippedHotbarIndex = 0;
    public float equippedOpacity = 0.9f;
    public float normalOpacity = 0.58f;
    public Transform hand;
    private GameObject currentHandItem;

    public GameObject itemDescriptionParent;
    public Image itemDescriptionImage;
    public TextMeshProUGUI descriptionItemNameText;
    public TextMeshProUGUI itemDescriptionText;
    public TextMeshProUGUI itemName;

    public GameObject craftingMenu;
    public GameObject armoryMenu;

    //Crafting
    public List<Recipe> allRecipes = new List<Recipe>();
    public Transform craftingGrid;
    public GameObject craftingButtonprefab;
    public GameObject itemNeededUIPrefab;

    // Player freeze controls
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour cameraScript;

    private List<Slot> inventorySlots = new List<Slot>();
    private List<Slot> hotbarSlots = new List<Slot>();
    private List<Slot> allSlots = new List<Slot>();
    private List<Slot> chestUISlots = new List<Slot>();

    private Slot draggedSlot = null;
    private bool isdragging = false;

    private StorageChest currentChest = null;
    private CharacterController playerController;
    private bool wasControllerEnabled = false;
    private bool wasMovementScriptEnabled = false;
    private bool wasCameraScriptEnabled = false;

    private void Awake()
    {
        // Initialize singleton
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        inventorySlots.AddRange(inventorySlotParent.GetComponentsInChildren<Slot>());
        hotbarSlots.AddRange(hotbarObj.GetComponentsInChildren<Slot>());
        chestUISlots.AddRange(chestUI.GetComponentsInChildren<Slot>());

        allSlots.AddRange(inventorySlots);
        allSlots.AddRange(hotbarSlots);

        GameObject pickupPromptObj = GameObject.FindWithTag("PickupPrompt");
        if (pickupPromptObj != null)
        {
            pickupPromptText = pickupPromptObj.GetComponentInChildren<TextMeshProUGUI>();
        }
        chestUI.gameObject.SetActive(false);

        // Get player controller reference
        playerController = FindAnyObjectByType<CharacterController>();

        if (playerController == null)
        {
            Debug.LogWarning("CharacterController not found in scene!");
        }
    }

    private void Start()
    {
        EquipHandItem();

        // Prompt UI is owned by Interactor now.
        if (pickupPromptText != null)
            pickupPromptText.enabled = false;

        if (lookedAtItemNameText != null)
            lookedAtItemNameText.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // Only close inventory if no chest is open
            if (currentChest == null)
            {
                container.SetActive(!container.activeInHierarchy);
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }
        }

        // Allow closing chest with Tab or Escape
        if (currentChest != null && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CloseChestUI();
        }

        StartDrag();
        UpdateDragItemPosition();
        EndDrag();

        HandleHotbarSelection();
        HandleDropEquippedItem();
        UpdateHotbarOpacity();
        UseHotbarItem();

        UpdateItemDescription();
    }

    public void PickupWorldItem(Item worldItem)
    {
        if (worldItem == null || worldItem.item == null)
            return;

        AddItem(worldItem.item, worldItem.amount);
        Destroy(worldItem.gameObject);
        EquipHandItem();
    }

    public void PickupNearbyItems(Vector3 center, float range)
    {
        Collider[] hits = Physics.OverlapSphere(center, range);
        bool pickedAny = false;

        // Collect first so we don't mutate while iterating physics results oddly.
        List<Item> items = new List<Item>();
        foreach (Collider hit in hits)
        {
            Item item = hit.GetComponentInParent<Item>();
            if (item == null)
                item = hit.GetComponent<Item>();

            if (item != null && item.item != null && !items.Contains(item))
                items.Add(item);
        }

        foreach (Item item in items)
        {
            AddItem(item.item, item.amount);
            Destroy(item.gameObject);
            pickedAny = true;
        }

        if (pickedAny)
            EquipHandItem();
    }

    public void AddItem(ItemSO itemToAdd, int amount)
    {
        int remainingAmount = amount;

        // First pass: try to add to existing stacks
        foreach (Slot slot in allSlots)
        {
            if (slot.HasItem() && slot.GetItem() == itemToAdd)
            {
                int currentAmount = slot.GetAmount();
                int maxStackSize = itemToAdd.maxStackSize;

                if (currentAmount < maxStackSize)
                {
                    int spaceLeft = maxStackSize - currentAmount;
                    int amountToAdd = Mathf.Min(spaceLeft, remainingAmount);

                    slot.SetItem(itemToAdd, currentAmount + amountToAdd);
                    remainingAmount -= amountToAdd;

                    if (remainingAmount <= 0)
                    {
                        PopulateCraftingGrid();
                        return;
                    }
                }
            }
        }

        // Second pass: fill empty slots
        foreach (Slot slot in allSlots)
        {
            if (!slot.HasItem())
            {
                int amountToPlace = Mathf.Min(itemToAdd.maxStackSize, remainingAmount);
                slot.SetItem(itemToAdd, amountToPlace);
                remainingAmount -= amountToPlace;

                if (remainingAmount <= 0)
                {
                    PopulateCraftingGrid();
                    return;
                }
            }
        }

        // Log if inventory is full
        if (remainingAmount > 0)
        {
            Debug.Log("Inventory is full, could not add " + remainingAmount + " of " + itemToAdd.itemName);
        }
        PopulateCraftingGrid();
    }

    private void StartDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Slot hovered = GetHoveredSlot();

            if (hovered != null && hovered.HasItem())
            {
                draggedSlot = hovered;
                isdragging = true;

                dragIcon.sprite = hovered.GetItem().icon;
                dragIcon.color = new Color(1, 1, 1, 0.5f);
                dragIcon.enabled = true;
            }
        }
    }

    private void EndDrag()
    {
        if (Input.GetMouseButtonUp(0) && isdragging)
        {
            Slot hovered = GetHoveredSlot();

            if (hovered != null)
            {
                HandleDrop(draggedSlot, hovered);

                dragIcon.enabled = false;

                draggedSlot = null;
                isdragging = false;
            }
        }
    }

    private Slot GetHoveredSlot()
    {
        // Check inventory and hotbar slots
        foreach (Slot slot in allSlots)
        {
            if (slot.hovering)
            {
                return slot;
            }
        }

        // Check chest UI slots
        foreach (Slot slot in chestUISlots)
        {
            if (slot.hovering)
            {
                return slot;
            }
        }

        return null;
    }

    private void HandleDrop(Slot from, Slot to)
    {
        if (from == to) return;

        if (to.HasItem() && to.GetItem() == from.GetItem())
        {
            int max = to.GetItem().maxStackSize;
            int space = max - to.GetAmount();

            if (space > 0)
            {
                int move = Mathf.Min(space, from.GetAmount());

                to.SetItem(to.GetItem(), to.GetAmount() + move);
                from.SetItem(from.GetItem(), from.GetAmount() - move);

                if (from.GetAmount() <= 0)
                    from.ClearSlot();

                return;
            }
        }

        //Different Item
        if (to.HasItem())
        {
            ItemSO tempItem = to.GetItem();
            int tempAmount = to.GetAmount();

            to.SetItem(from.GetItem(), from.GetAmount());
            from.SetItem(tempItem, tempAmount);
            return;
        }

        //Empty Slot
        to.SetItem(from.GetItem(), from.GetAmount());
        from.ClearSlot();
    }

    private void UpdateDragItemPosition()
    {
        dragIcon.transform.position = Input.mousePosition;
    }

    private void UpdateHotbarOpacity()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            Image icon = hotbarSlots[i].GetComponent<Image>();
            if (icon != null)
            {
                icon.color = (i == equippedHotbarIndex) ? new Color(1, 1, 1, equippedOpacity) : new Color(1, 1, 1, normalOpacity);
            }
        }
    }

    private void HandleHotbarSelection()
    {
        for (int i = 0; i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(key))
            {
                if (equippedHotbarIndex == i)
                    continue;

                equippedHotbarIndex = i;
                UpdateHotbarOpacity();
                EquipHandItem();
            }
        }
    }

    private void HandleDropEquippedItem()
    {
        if (!Input.GetKeyDown(KeyCode.Q)) return;

        Slot equippedSlot = hotbarSlots[equippedHotbarIndex];

        if (!equippedSlot.HasItem()) return;

        ItemSO itemSO = equippedSlot.GetItem();
        GameObject prefab = itemSO.itemPrefab;

        if (prefab == null) return;

        GameObject dropped = Instantiate(prefab, Camera.main.transform.position + Camera.main.transform.forward, Quaternion.identity);

        Item item = dropped.GetComponent<Item>();
        item.item = itemSO;
        item.amount = equippedSlot.GetAmount();

        equippedSlot.ClearSlot();
        EquipHandItem();
    }

    private void EquipHandItem()
    {
        ClearCurrentHandItem();

        Slot equippedSlot = hotbarSlots[equippedHotbarIndex];
        if (!equippedSlot.HasItem())
        {
            NotifyCombatSystem(null, null);
            return;
        }

        ItemSO item = equippedSlot.GetItem();
        if (item.handItemPrefab == null)
        {
            NotifyCombatSystem(null, item);
            return;
        }

        currentHandItem = Instantiate(item.handItemPrefab, hand);
        currentHandItem.transform.localPosition = Vector3.zero;
        currentHandItem.transform.localRotation = Quaternion.identity;

        NotifyCombatSystem(currentHandItem, item);
    }

    /// <summary>
    /// Destroys the held item and cancels any active placement ghost first.
    /// Fixes leftover ghost materials when switching hotbar slots mid-placement.
    /// </summary>
    private void ClearCurrentHandItem()
    {
        if (currentHandItem == null)
            return;

        PlaceableObject placeable = currentHandItem.GetComponentInChildren<PlaceableObject>();
        if (placeable != null)
            placeable.CancelPlacement();

        Destroy(currentHandItem);
        currentHandItem = null;
    }

    private void NotifyCombatSystem(GameObject handItem, ItemSO item)
    {
        CharacterMovementController movement = FindAnyObjectByType<CharacterMovementController>();
        if (movement != null)
        {
            // RefreshEquippedItem method doesn't exist on CharacterMovementController
            // This section needs to be updated based on your combat system implementation
        }
    }

    private void UpdateItemDescription()
    {
        Slot hoveredSlot = GetHoveredSlot();
        if (hoveredSlot != null)
        {
            ItemSO hoveredItem = hoveredSlot.GetItem();
            if (hoveredItem != null)
            {
                itemDescriptionParent.SetActive(true);
                itemDescriptionImage.sprite = hoveredItem.icon;
                itemDescriptionText.text = hoveredItem.description;
                descriptionItemNameText.text = hoveredItem.itemName;
                return;
            }
        }
        itemDescriptionParent.SetActive(false);
    }

    private void UseHotbarItem()
    {
        if (!Input.GetKeyDown(KeyCode.X)) return;

        Slot equippedSlot = hotbarSlots[equippedHotbarIndex];

        if (!equippedSlot.HasItem()) return;

        ItemSO item = equippedSlot.GetItem();

        if (!item.isConsumable) return;

        item.UseItem();

        int newAmount = equippedSlot.GetAmount() - 1;

        if (newAmount <= 0)
        {
            equippedSlot.ClearSlot();
        }
        else
        {
            equippedSlot.SetItem(item, newAmount);
        }

        EquipHandItem();
    }

    private void PopulateCraftingGrid()
    {
        for (int i = craftingGrid.childCount - 1; i >= 0; i--)
        {
            Destroy(craftingGrid.GetChild(i).gameObject);
        }

        foreach (Recipe recipe in allRecipes)
        {
            if (!CanCraft(recipe))
            {
                continue;
            }

            GameObject buttonObj = Instantiate(craftingButtonprefab, craftingGrid);
            Image img = buttonObj.transform.GetChild(0).GetComponent<Image>();
            img.sprite = recipe.result.icon;

            Button button = buttonObj.GetComponent<Button>();

            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Craft(recipe));

            foreach (Ingredient ingredient in recipe.ingredients)
            {
                GameObject neededItem = Instantiate(itemNeededUIPrefab, buttonObj.transform.GetChild(1));
                neededItem.GetComponent<Image>().sprite = ingredient.item.icon;
                neededItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "x" + ingredient.amount.ToString();
            }
        }
    }

    public void Craft(Recipe recipe)
    {
        if (!CanCraft(recipe))
        {
            return;
        }
        ConsumeIngredients(recipe);
        AddItem(recipe.result, recipe.resultAmount);

        PopulateCraftingGrid();
    }

    private void ConsumeIngredients(Recipe recipe)
    {
        foreach (Ingredient ingredient in recipe.ingredients)
        {
            int remaining = ingredient.amount;

            foreach (Slot slot in allSlots)
            {
                if (!slot.HasItem()) continue;
                if (slot.GetItem() != ingredient.item) continue;

                int take = Mathf.Min(slot.GetAmount(), remaining);
                slot.SetItem(slot.GetItem(), slot.GetAmount() - take);

                if (slot.GetAmount() <= 0)
                    slot.ClearSlot();

                remaining -= take;
                if (remaining <= 0)
                    break;
            }
        }
    }

    public bool CanCraft(Recipe recipe)
    {
        foreach (Ingredient ingredient in recipe.ingredients)
        {
            int totalFound = 0;

            foreach (Slot slot in allSlots)
            {
                if (slot.HasItem() && slot.GetItem() == ingredient.item)
                {
                    totalFound += slot.GetAmount();
                }
            }
            if (totalFound < ingredient.amount)
                return false;
        }
        return true;
    }

    // Public methods to manage chest interaction
    public void OpenChestUI(StorageChest chest)
    {
        currentChest = chest;
        container.SetActive(true);
        chestUI.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisableOtherMenus();
        // Freeze player movement
        FreezePlayer(true);
    }

    public void CloseChestUI()
    {
        if (currentChest != null)
        {
            currentChest.SaveChestItems();
            currentChest = null;
        }
        chestUI.gameObject.SetActive(false);
        container.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;

        // Unfreeze player movement
        FreezePlayer(false);
    }

    private void DisableOtherMenus()
    {
        if (craftingMenu != null && craftingMenu.activeInHierarchy)
        {
            craftingMenu.SetActive(false);
            Debug.Log("Crafting menu disabled");
        }

        if (armoryMenu != null && armoryMenu.activeInHierarchy)
        {
            armoryMenu.SetActive(false);
            Debug.Log("Armory menu disabled");
        }
    }

    public void FreezePlayer(bool freeze)
    {
        Debug.Log("Inventory.FreezePlayer called: " + freeze);

        if (playerController == null)
        {
            Debug.LogWarning("CharacterController not found!");
            return;
        }

        if (freeze)
        {
            wasControllerEnabled = playerController.enabled;
            playerController.enabled = false;
            Debug.Log("Player frozen - Controller disabled");

            if (playerMovementScript != null)
            {
                wasMovementScriptEnabled = playerMovementScript.enabled;
                playerMovementScript.enabled = false;
                Debug.Log("PlayerMovement script disabled");
            }
            else
            {
                Debug.LogWarning("playerMovementScript is NULL!");
            }

            if (cameraScript != null)
            {
                wasCameraScriptEnabled = cameraScript.enabled;
                cameraScript.enabled = false;
                Debug.Log("Camera script disabled");
            }
            else
            {
                Debug.LogWarning("cameraScript is NULL!");
            }
        }
        else
        {
            playerController.enabled = wasControllerEnabled;
            Debug.Log("Player unfrozen - Controller re-enabled");

            if (playerMovementScript != null)
            {
                playerMovementScript.enabled = wasMovementScriptEnabled;
            }

            if (cameraScript != null)
            {
                cameraScript.enabled = wasCameraScriptEnabled;
            }
        }
    }

    public void ConsumeItem()
    {
        Slot equippedSlot = hotbarSlots[equippedHotbarIndex];

        if (!equippedSlot.HasItem()) return;

        int newAmount = equippedSlot.GetAmount() - 1;

        if (newAmount <= 0)
        {
            equippedSlot.ClearSlot();
        }
        else
        {
            equippedSlot.SetItem(equippedSlot.GetItem(), newAmount);
        }

        EquipHandItem();
    }

    public bool HasEquippedItem()
    {
        Slot equippedSlot = hotbarSlots[equippedHotbarIndex];
        return equippedSlot.HasItem();
    }
}
