# 日常动作图集生成提示词

## 灰猫纯灰毛色修订

用户确认灰猫为纯灰短毛。当前灰猫素材使用内置 imagegen 按以下提示词修正。

```text
Edit target: the supplied 6-column by 4-row cat animation sprite atlas. Change ONLY the cat's coat coloration in EVERY ONE of the 24 existing sprites to a SOLID BLUE-GRAY British Shorthair coat. Erase all dark tabby markings everywhere: all horizontal or wavy flank and back stripes, tail rings, leg bands, chest necklaces, forehead M marks, cheek stripes and belly spots. Fill those markings with the same even blue-gray fur color as the surrounding coat. Retain subtle individual short fur hairs, soft broad volume shading, the original gold/amber eyes, pink inner ears and paw pads, nose, whiskers, all anatomy and expressions. The result must clearly be an entirely plain uniform gray cat with soft dimensional shading in all 24 poses. Preserve the exact original 1536x1024 canvas, 6x4 layout, each sprite's precise silhouette, orientation, scale, location, foot baseline and pose and all inter-frame motion. This is a strict coat recolor of an existing game atlas. Background everywhere is genuinely fully transparent with alpha=0. No floor, glow, shadows, text, bowls or grid lines. Preserve alpha transparency. Check every sprite including rolling back/belly, all walking frames and the tail.
```

图集由内置 imagegen 生成，参考原图 assets/cats.png。黑猫透明背景清理后的图集同时作为布偶猫和灰猫的姿势布局参考。运行时按固定格子读取，并清理低透明度背景及相邻格的离体碎片。

输出素材为 `daily-black.png`、`daily-ragdoll.png`、`daily-gray.png` 和 `daily-bowls.png`，均位于本目录。

## 黑猫

```text
Create a production sprite animation atlas for a Windows desktop pet. The supplied image is the identity and drawing-style reference. Use ONLY the left black tuxedo cat, with its green eyes, white chest bib, white toes and distinctive white curved marking beside its mouth. Preserve its face proportions, coat pattern, fluffy silhouette and the reference's detailed pixel-painted fur style.
Create exactly 24 full-body cat sprites in a perfectly regular 6-column by 4-row grid on a true transparent background. Canvas 1536 x 1024, each cell 256 x 256. No grid lines, no text, no labels, no shadows or scenery. Every sprite fits completely inside its own cell with transparent margins. Keep the same physical scale across ALL poses. Ground contact baseline at y=224 within every cell, centered x=128. Sitting cat height about 200px. Stretching and rolling should use physically plausible articulated cat poses, with coherent four paws, neck, spine and tail. Gentle and relaxed, suitable for a continuous idle animation.
Read cells left to right then top to bottom in this EXACT order.
0 neutral seated, matching reference.
1 anticipation for a stretch, slight forward lean.
2 front paws reach forward, head lowers, back lengthens.
3 full natural cat stretch, front legs long, chest low, hindquarters raised.
4 hold stretch with a tiny relaxation.
5 paws retract, body begins to sit up.
6 neutral seated matching frame 0.
7 anticipation for rolling, leaning to its left and curling paws.
8 lying on left flank with paws relaxed.
9 rolls onto back, belly and four curled paws visible.
10 rolls to the other flank.
11 pushes up and gathers paws to sit.
12 neutral seated matching frame 0.
13 right-facing three-quarter walking pose, stride phase 1.
14 same right-facing walking, stride phase 2.
15 same right-facing walking, stride phase 3.
16 same right-facing walking, stride phase 4, seamlessly loops to 13.
17 right-facing crouch, lowering head to drink at an imaginary low bowl.
18 same crouch, a subtle lapping pose.
19 same crouch, alternate subtle lapping pose.
20 raises head after drinking.
21 right-facing crouch, lowers head to eat from an imaginary low bowl.
22 same crouch, small nibbling pose.
23 raises head after eating.
Draw only the cat, with no bowl in any cell. Preserve the exact tuxedo identity and original illustration style throughout. All frame transitions should be small and physically consistent. True alpha transparency.
```

## 黑猫透明背景清理

```text
Background extraction only. Edit this 6 by 4 cat sprite atlas. Remove the entire pale white/gray backdrop and all faint ground shadows from every cell, including the outer margins and the space between all 24 cats. Output a PNG with REAL FULL TRANSPARENCY: background alpha must be exactly 0, not translucent white, not a checkerboard. Cat interiors should remain opaque. Preserve all 24 existing cat drawings, poses, fur colors, cell positions, canvas size and layout exactly. Do not redraw or restyle any cat. Keep fine whisker edges with clean antialiasing. This is a game sprite atlas that must render on arbitrary dark desktop backgrounds.
```

## 布偶猫

```text
Create a production sprite animation atlas for a Windows desktop pet. Image 1 supplies character identity and original pixel-painted fur illustration style. Image 2 supplies ONLY the exact 24-pose layout, anatomy and pose sequence to follow. Use ONLY the middle ragdoll cat from reference image 1. Preserve its long fluffy cream and brown coat, brown ears and face mask, white facial blaze and muzzle, large white chest and paws, blue eyes, broad fluffy body and curled brown tail. Replace every black cat in the layout with this one consistent character. Preserve the source character proportions and coat markings across every frame. Create exactly 24 full-body cat sprites in a perfectly regular 6-column by 4-row grid on a true transparent background. Canvas 1536 x 1024, each cell 256 x 256. No grid lines, no text, no labels, no shadows or scenery. Every sprite fits completely inside its own cell with transparent margins. Keep the same physical scale across ALL poses. Ground contact baseline at y=224 within every cell, centered x=128. Sitting cat height about 200px. Stretching and rolling should use physically plausible articulated cat poses, with coherent four paws, neck, spine and tail. Gentle and relaxed, suitable for a continuous idle animation.
Read cells left to right then top to bottom in this EXACT order.
0 neutral seated, matching reference.
1 anticipation for a stretch, slight forward lean.
2 front paws reach forward, head lowers, back lengthens.
3 full natural cat stretch, front legs long, chest low, hindquarters raised.
4 hold stretch with a tiny relaxation.
5 paws retract, body begins to sit up.
6 neutral seated matching frame 0.
7 anticipation for rolling, leaning to its left and curling paws.
8 lying on left flank with paws relaxed.
9 rolls onto back, belly and four curled paws visible.
10 rolls to the other flank.
11 pushes up and gathers paws to sit.
12 neutral seated matching frame 0.
13 right-facing three-quarter walking pose, stride phase 1.
14 same right-facing walking, stride phase 2.
15 same right-facing walking, stride phase 3.
16 same right-facing walking, stride phase 4, seamlessly loops to 13.
17 right-facing crouch, lowering head to drink at an imaginary low bowl.
18 same crouch, a subtle lapping pose.
19 same crouch, alternate subtle lapping pose.
20 raises head after drinking.
21 right-facing crouch, lowers head to eat from an imaginary low bowl.
22 same crouch, small nibbling pose.
23 raises head after eating.
Draw only the cat, with no bowl in any cell. Preserve the exact reference cat identity and original illustration style throughout. All frame transitions should be small and physically consistent. True alpha transparency.
CRITICAL true transparency. All empty areas and margins must have alpha exactly 0, with opaque cat interiors. Do not render a pale backdrop or a translucent colored background. Keep the regular 1536x1024 canvas with 6 columns and 4 rows.
```

## 灰猫

```text
Create a production sprite animation atlas for a Windows desktop pet. Image 1 supplies character identity and original pixel-painted fur illustration style. Image 2 supplies ONLY the exact 24-pose layout, anatomy and pose sequence to follow. Use ONLY the right solid-gray British Shorthair cat from reference image 1. Preserve its dense uniformly blue-gray short fur, round broad face, yellow-gold eyes, gray muzzle and paws, rounded sturdy body and plain gray curled tail. The entire coat is solid gray with soft volume shading and fine fur texture. Every frame must have an unmarked gray forehead, cheeks, chest, belly, back, legs and tail. Replace every black cat in the layout with this one consistent character. Preserve the source character proportions and solid coat across every frame. Create exactly 24 full-body cat sprites in a perfectly regular 6-column by 4-row grid on a true transparent background. Canvas 1536 x 1024, each cell 256 x 256. No grid lines, no text, no labels, no shadows or scenery. Every sprite fits completely inside its own cell with transparent margins. Keep the same physical scale across ALL poses. Ground contact baseline at y=224 within every cell, centered x=128. Sitting cat height about 200px. Stretching and rolling should use physically plausible articulated cat poses, with coherent four paws, neck, spine and tail. Gentle and relaxed, suitable for a continuous idle animation.
Read cells left to right then top to bottom in this EXACT order.
0 neutral seated, matching reference.
1 anticipation for a stretch, slight forward lean.
2 front paws reach forward, head lowers, back lengthens.
3 full natural cat stretch, front legs long, chest low, hindquarters raised.
4 hold stretch with a tiny relaxation.
5 paws retract, body begins to sit up.
6 neutral seated matching frame 0.
7 anticipation for rolling, leaning to its left and curling paws.
8 lying on left flank with paws relaxed.
9 rolls onto back, belly and four curled paws visible.
10 rolls to the other flank.
11 pushes up and gathers paws to sit.
12 neutral seated matching frame 0.
13 right-facing three-quarter walking pose, stride phase 1.
14 same right-facing walking, stride phase 2.
15 same right-facing walking, stride phase 3.
16 same right-facing walking, stride phase 4, seamlessly loops to 13.
17 right-facing crouch, lowering head to drink at an imaginary low bowl.
18 same crouch, a subtle lapping pose.
19 same crouch, alternate subtle lapping pose.
20 raises head after drinking.
21 right-facing crouch, lowers head to eat from an imaginary low bowl.
22 same crouch, small nibbling pose.
23 raises head after eating.
Draw only the cat, with no bowl in any cell. Preserve the exact reference cat identity and original illustration style throughout. All frame transitions should be small and physically consistent. True alpha transparency.
CRITICAL true transparency. All empty areas and margins must have alpha exactly 0, with opaque cat interiors. Do not render a pale backdrop or a translucent colored background. Keep the regular 1536x1024 canvas with 6 columns and 4 rows.
```

## 水碗与饭碗

```text
Create a clean game sprite atlas containing exactly TWO small pet bowls in one row, on a real transparent background with background alpha exactly zero. Match the reference cat image's detailed soft pixel-painted illustration style, warm natural outlines and gentle top-front three-quarter viewpoint. Left half contains one muted dusty-blue ceramic water bowl with a visible ellipse of clear light-blue water and one tiny highlight. Right half contains one warm terracotta ceramic food bowl filled with small brown dry kibble pieces. These are practical tiny desktop-pet props, with simple readable silhouettes and low shallow bowl profiles, no decorations, no labels, no logos, no floor, no cats, no background gradients, no cast shadows. Both bowls should have the same apparent diameter and stand on the same baseline. Use a 1536x1024 canvas, the water bowl fully inside the left 768px half, food fully inside the right half, centered within its half. Each bowl roughly 580px wide and 220px tall, full body and rim visible, with generous completely transparent margins. Make the silhouettes crisp enough to read when scaled down to 45–80 pixels wide.
```
