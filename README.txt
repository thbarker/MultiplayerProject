This is Teddy Barker's Multiplayer Project for CS 596.
Below is a tutorial on how to play the game, followed by some technical details about the project.

Tutorial:
The goal of this mini game is to beat your opponent in a shooting contest, where each player takes turns
evading the other player's single shot. If struck in the head, the opponent will earn a point, and if 
the time is up or the opponent misses, no point will be awarded. First player to go is randomly selected, 
and the winner is the first to 4 points (Best of 7). The game will restart after a few moments when a victor
is selected. Below are the controls. 

Left Mouse Button - Fire
Mouse             - Aim/Look Around
A                 - Move Left
D                 - Move Right

Have Fun!

Note about building:
I noticed an issue when building the game off a fresh pull or boot of the unity editor. If the client is unable to connect
(Where the Screen is just red and it says Waiting for players) a fix for this is to:
  
1. Move the Player2.0 Prefab from the Prefabs folder into the scene
2. Apply Overrides (Top of the Inspector window when selected)
3. Delete the Player2.0 from the scene.
4. Drag the Player2.0 prefab from the Prefabs folder to the PlayerSpawner's Player Prefab reference in the scene. (Even if it already shows the Prefab to be referenced)

I don't know why this happens and I hope it doesn't happen when you first try to build the game, but in case it does, you should be able to resolve it quickly.

Thank you for reading and enjoy the game!
