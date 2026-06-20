# Brave Browser Profile Migration: Community Research Notes

Source: Brave Community Forum (community.brave.com)
Thread period: January 31 to February 5, 2020
Closed: March 6, 2020 (automatically closed after 30 days of no activity)
Participants: antongrund (user reporting the issue), Aa-ron (Brave Support staff)


## What the User Was Trying to Do

A Brave user named antongrund wanted to copy an existing browser profile over to a new one. The goal was simple: carry over saved settings and configurations without having to set everything up again from scratch. This is a fairly common need, especially for people who manage multiple browser profiles for different purposes.


## How Brave Stores Profiles

Before getting into what happened, it helps to understand how Brave actually organizes its profiles on disk. Each profile lives in its own folder inside the browser's data directory. The naming works like this:

- The very first profile you get when you install Brave is always called **Default**. You never chose to create it; it just exists.
- The first time you manually create a new profile, Brave names that folder **Profile 1**.
- Every profile you create after that continues the count: Profile 2, Profile 3, and so on.

So if you see folders named Default, Profile 1, and Profile 9 in your data directory, that means three profiles exist and nine have been created in total over the life of that installation. Each folder holds everything for that profile: bookmarks, extensions, history, settings, and so on.


## What Support Recommended

On February 4, 2020, Aa-ron from Brave Support stepped in to clarify the profile structure and offer a solution. The guidance was straightforward: since antongrund had identified Profile 1 as the profile they wanted to copy, they could simply copy the contents of that folder into whichever destination profile folder they wanted. No special tools, just a manual folder copy.

This is the officially supported approach and works for most people.


## What Actually Happened

The folder copy approach did not work for antongrund. No specific error was mentioned in the thread, but the result was not what they expected. After some back and forth, they decided to do a clean install of Brave Browser instead, and that resolved the issue. The browser then worked exactly as the support staff had described.

It is worth noting that clean installs sometimes fix problems that manual migrations do not, likely because a fresh installation avoids any corrupted preferences or leftover conflicts from the previous setup.


## What the User Suggested

After resolving the issue, antongrund made a reasonable observation: when you create a new profile in Brave, there is no option to inherit settings from an existing one. You always start from a blank slate. The user suggested it would be helpful to have a "start with the same settings as another profile" option during profile creation. As of the time of this writing, this does not appear to be a built-in feature.


## Key Takeaways

The manual folder copy method is the officially recommended way to migrate a Brave profile. It works for most users, but a clean install may be needed if the copy method produces unexpected results. New profiles do not share settings with existing ones by default, which means anyone managing multiple profiles has to configure each one individually unless they copy the folder manually.


## Related Threads Worth Looking At

The Brave community has discussed variations of this problem across several threads. These are worth reviewing if the above does not fully address your situation:

- Synchronising multiple profiles on a single device (tags: linux, sync)
- Migrating all profiles to a new MacBook (Jun 2020)
- Exporting browser settings (Jul 2020)
- Moving Brave profiles to a new Windows computer (Feb 2020)
- Syncing multiple profiles across Windows, Android, and macOS (Nov 2025)
- Transferring or copying a profile (Aug 2022)
- Exporting data including extensions (Oct 2020)
- Copying profiles on macOS, another angle (Dec 2023)

The November 2025 thread on syncing multiple profiles is particularly worth checking, as it likely reflects more current behavior and may surface options that did not exist from the year 2020.


## A Note on This Document

This write-up is based entirely on a community support thread from early 2020. Browser internals change over time, so the folder names and copy behavior described here may differ from what you see in a current version of Brave. Treat this as a starting point for investigation, not a final answer.

Compiled from Brave Community Forum, January to February 2020.