using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The cash shop window art (UI/UICashShop.img) and its item data. The file
// names must match MushTown.Client/Panels/HudCashShop.cs.
public static partial class Export
{
    public static bool ExportCashShopUi(WzRoot ui, WzRoot item, WzRoot str, string outDir)
    {
        var catalog = ExportList.LoadCashShop();
        if (!catalog.ValidateShape(out string shapeError))
        {
            Console.WriteLine(shapeError);
            return false;
        }

        var files = new List<(string Node, string File)>
        {
            ("Basic.img/VScr111/enabled/base", "wm_scroll_base.png"),
            ("Basic.img/VScr111/enabled/thumb0", "wm_scroll_thumb.png"),
            ("UICashShop.img/mainView/layer:bg", "cashshop_scene_bg.png"),
            ("UICashShop.img/base/layer:bgSearch", "cashshop_bg_search.png"),
            ("UICashShop.img/base/layer:bgCash", "cashshop_bg_cash.png"),
            ("UICashShop.img/base/layer:bgMaplePoint", "cashshop_bg_maplepoint.png"),
            ("UICashShop.img/base/layer:bgMileage", "cashshop_bg_mileage.png"),
            ("UICashShop.img/listView/layer:bg", "cashshop_list_bg.png"),
            ("UICashShop.img/listView/layer:bgList", "cashshop_bg_list.png"),
            ("UICashShop.img/listView/layer:noResult", "cashshop_no_result.png"),
            ("UICashShop.img/itemBox/canvas:itemBoxBg", "cashshop_item_bg.png"),
            ("UICashShop.img/itemBox/canvas:itemBoxBgMain", "cashshop_item_bg_main.png"),
            ("UICashShop.img/itemBox/BtBuy/normal/0", "cashshop_item_price_n.png"),
            ("UICashShop.img/itemBox/BtBuy/mouseOver/0", "cashshop_item_price_h.png"),
            ("UICashShop.img/itemBox/BtBuy/pressed/0", "cashshop_item_price_p.png"),
            ("UICashShop.img/avatarView/layer:preview_back0", "cashshop_preview_back.png"),
            ("UICashShop.img/avatarView/layer:preview_bottom", "cashshop_preview_bottom.png"),
            ("UICashShop.img/avatarView/layer:backGround_frame", "cashshop_preview_frame.png"),
            ("UICashShop.img/previewAvatarBox/canvas:itemBoxBg", "cashshop_cart_item_bg.png"),
            ("UICashShop.img/menu/500/layer:bg", "cashshop_menu_500.png"),
            ("UICashShop.img/menu/600/layer:bg", "cashshop_menu_600.png"),
            ("UICashShop.img/menu/700/layer:bg", "cashshop_menu_700.png"),
            ("UICashShop.img/menu/100/layer:bg", "cashshop_menu_100.png"),
            ("UICashShop.img/beautyView/700/layer:bg", "cashshop_beauty_bg.png"),
            ("UICashShop.img/beautyView/700/layer:list", "cashshop_beauty_list.png"),
            ("UICashShop.img/ConfirmPurchase/single/layer:bg", "cashshop_checkout_single.png"),
            ("UICashShop.img/ConfirmPurchase/multi/layer:bg", "cashshop_checkout_multi.png"),
            ("UICashShop.img/ConfirmPurchase/Entity/backgrnd", "cashshop_checkout_entity.png"),
            ("UICashShop.img/ConfirmPurchase/Entity/layer:iconBg", "cashshop_checkout_icon_bg.png"),
            ("UICashShop.img/ConfirmPurchase/btBuy/normal/0", "cashshop_checkout_buy_n.png"),
            ("UICashShop.img/ConfirmPurchase/btBuy/mouseOver/0", "cashshop_checkout_buy_h.png"),
            ("UICashShop.img/ConfirmPurchase/btBuy/pressed/0", "cashshop_checkout_buy_p.png"),
            ("UICashShop.img/ConfirmPurchase/btCancel/normal/0", "cashshop_checkout_cancel_n.png"),
            ("UICashShop.img/ConfirmPurchase/btCancel/mouseOver/0", "cashshop_checkout_cancel_h.png"),
            ("UICashShop.img/ConfirmPurchase/btCancel/pressed/0", "cashshop_checkout_cancel_p.png"),
            ("UICashShop.img/ConfirmPurchase/btGift/disabled/0", "cashshop_checkout_gift_n.png"),
            ("UICashShop.img/ConfirmPurchase/btGift/mouseOver/0", "cashshop_checkout_gift_h.png"),
            ("UICashShop.img/ConfirmPurchase/btGift/pressed/0", "cashshop_checkout_gift_p.png"),
            ("UICashShop.img/ConfirmPurchase/mesoCashResult", "cashshop_checkout_result.png"),
            ("UICashShop.img/base/button:exit/normal/0", "cashshop_exit.png"),
            ("UICashShop.img/base/button:exit/disabled/0", "cashshop_exit_disabled.png"),
            ("UICashShop.img/base/button:help/normal/0", "cashshop_help.png"),
            ("UICashShop.img/base/button:help/disabled/0", "cashshop_help_disabled.png"),
            ("UICashShop.img/base/button:probability/normal/0", "cashshop_probability.png"),
            ("UICashShop.img/base/button:probability/disabled/0", "cashshop_probability_disabled.png"),
            ("UICashShop.img/base/button:coupon/normal/0", "cashshop_coupon.png"),
            ("UICashShop.img/base/button:coupon/disabled/0", "cashshop_coupon_disabled.png"),
            ("UICashShop.img/base/button:search/normal/0", "cashshop_search.png"),
            ("UICashShop.img/avatarView/button:DefaultAvatar/normal/0", "cashshop_defaultavatar_n.png"),
            ("UICashShop.img/avatarView/button:DefaultAvatar/mouseOver/0", "cashshop_defaultavatar_h.png"),
            ("UICashShop.img/avatarView/button:DefaultAvatar/pressed/0", "cashshop_defaultavatar_p.png"),
            ("UICashShop.img/avatarView/button:DefaultAvatar/disabled/0", "cashshop_defaultavatar_disabled.png"),
            ("UICashShop.img/avatarView/button:TakeoffAvatar/normal/0", "cashshop_takeoffavatar_n.png"),
            ("UICashShop.img/avatarView/button:TakeoffAvatar/mouseOver/0", "cashshop_takeoffavatar_h.png"),
            ("UICashShop.img/avatarView/button:TakeoffAvatar/pressed/0", "cashshop_takeoffavatar_p.png"),
            ("UICashShop.img/avatarView/button:SaveAvatar/normal/0", "cashshop_saveavatar.png"),
            ("UICashShop.img/avatarView/button:MyCoordi/normal/0", "cashshop_mycoordi.png"),
            ("UICashShop.img/avatarView/button:actionPlay/normal/0", "cashshop_actionplay.png"),
            ("UICashShop.img/avatarView/button:petOn/normal/0", "cashshop_peton.png"),
            ("UICashShop.img/avatarView/button:petOff/normal/0", "cashshop_petoff.png"),
            ("UICashShop.img/avatarView/button:BuyAvatar/normal/0", "cashshop_buyavatar_n.png"),
            ("UICashShop.img/avatarView/button:BuyAvatar/mouseOver/0", "cashshop_buyavatar_h.png"),
            ("UICashShop.img/avatarView/button:BuyAvatar/pressed/0", "cashshop_buyavatar_p.png"),
            ("UICashShop.img/avatarView/button:BuyAvatar/disabled/0", "cashshop_buyavatar_disabled.png"),
        };

        for (int group = 100; group <= 1000; group += 100)
        {
            // Main is not a shop page: its normal image is the gray tab in menu/200.
            string menu = group == 100 ? "200" : "100";
            files.Add(($"UICashShop.img/menu/{menu}/{group}/normal/0", $"cashshop_group_{group}.png"));
            if (group is 500 or 600 or 700)
                files.Add(($"UICashShop.img/menu/{group}/{group}/normal/0", $"cashshop_group_{group}_sel.png"));
        }

        for (int i = 0; i < 10; i++)
        {
            files.Add(($"UICashShop.img/menu/500/sub/{i}/normal/0", $"cashshop_sub_500_{i}.png"));
            files.Add(($"UICashShop.img/menu/500/sub/{i}/mouseOver/0", $"cashshop_sub_500_{i}_sel.png"));
        }
        for (int i = 0; i < 10; i++)
        {
            files.Add(($"UICashShop.img/menu/600/sub/{i}/normal/0", $"cashshop_sub_600_{i}.png"));
            files.Add(($"UICashShop.img/menu/600/sub/{i}/mouseOver/0", $"cashshop_sub_600_{i}_sel.png"));
        }
        for (int i = 0; i < 4; i++)
        {
            files.Add(($"UICashShop.img/menu/700/sub/{i}/normal/0", $"cashshop_sub_700_{i}.png"));
            files.Add(($"UICashShop.img/menu/700/sub/{i}/mouseOver/0", $"cashshop_sub_700_{i}_sel.png"));
        }

        foreach (var (node, file) in files)
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;

        foreach (var entry in catalog.Items)
        {
            if (entry.Group == "Utility")
            {
                string nodePath = $"Cash/{entry.Id / 10000:D4}.img/{entry.Id:D8}";
                string name = str.Find($@"Cash.img\{entry.Id}\name")?.GetValueEx<string>("") ?? "";
                if (name == "")
                {
                    Console.WriteLine($"cashshop item {entry.Id} has no String name");
                    return false;
                }
                if (item.Find(nodePath) == null)
                {
                    Console.WriteLine($"cashshop item {entry.Id} has no Item/Cash node");
                    return false;
                }
                var data = new ItemData
                {
                    ItemId = entry.Id,
                    Name = name,
                    Image = $"cashitem/{entry.Id}.png",
                    Desc = CleanDesc(str.Find($@"Cash.img\{entry.Id}\desc")?.GetValueEx<string>("") ?? "", "").Replace("\\r", ""),
                    Cash = true,
                };
                if (!UiPng.Save(item, "Item", $"{nodePath}/info/icon", Path.Combine(outDir, "cashitem", $"{entry.Id}.png")))
                    return false;
                WriteJson(Path.Combine(outDir, "cashitem", $"{entry.Id}.json"), data);
            }
            else if (!File.Exists(Path.Combine(outDir, "item", $"{entry.Id}.json")))
            {
                Console.WriteLine($"cashshop fashion item {entry.Id} has no exported item json");
                return false;
            }
        }

        // Every coupon icon resolves to the shared hair/face coupon icon.
        foreach (var (looks, iconId, kind) in new[] { (catalog.Hairs, 5157053, "hair"), (catalog.Faces, 5158018, "face") })
        {
            foreach (var look in looks)
            {
                string nodePath = $"Cash/0515.img/{look.ItemId:D8}";
                if (item.Find(nodePath) == null)
                {
                    Console.WriteLine($"cashshop look {look.ItemId} has no Item/Cash node");
                    return false;
                }
                int cosmetic = Int(item.Find($"{nodePath}/spec"), "cosmetic");
                if (cosmetic != look.Cosmetic)
                {
                    Console.WriteLine($"cashshop look {look.ItemId} gives cosmetic {cosmetic}, expected {look.Cosmetic}");
                    return false;
                }
                var data = new ItemData
                {
                    ItemId = look.ItemId,
                    Name = look.Name,
                    Image = $"cashitem/{look.ItemId}.png",
                    Desc = CleanDesc(str.Find($@"Cash.img\{look.ItemId}\desc")?.GetValueEx<string>("") ?? "", kind).Replace("\\r", ""),
                    Cash = true,
                    Cosmetic = look.Cosmetic,
                };
                if (!UiPng.Save(item, "Item", $"Cash/0515.img/{iconId:D8}/info/icon", Path.Combine(outDir, "cashitem", $"{look.ItemId}.png")))
                    return false;
                WriteJson(Path.Combine(outDir, "cashitem", $"{look.ItemId}.json"), data);
            }
        }

        WriteJson(Path.Combine(outDir, "cashshop", "catalog.json"), catalog);
        Console.WriteLine($"cashshop: {files.Count} ui files, {catalog.Items.Count} items, {catalog.Hairs.Count + catalog.Faces.Count} looks");
        return true;
    }
}
