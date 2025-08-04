![](https://acdb.tv/static/img/header_posters_1100px.jpg)

# ACdb.tv Jellyfin Plugin

[ACdb.tv](https://acdb.tv) is a webapp and plugin that updates your Jellyfin or Emby server with dynamic, auto-updating collections. ACdb.tv works for both Jellyfin and Emby. This is the official **Jellyfin** plugin.  Please [check out ACdb.tv](https://acdb.tv) for more information.

## What about Emby
For **Emby** installation, please use the Emby plugin catalog. [More information](https://acdb.tv/getting-started).

## Jellyfin Installation

1. On your Jellyfin server, go to **Settings  → Dashboard → Catalogue**.
    - ![](https://acdb.tv/static/img/help/getting-started/jellyfin/Jellyfin-plugin-catalog.jpg)
2. Click **Settings icon** at the top of the page to See your plugin Repositories.
    - ![](https://acdb.tv/static/img/help/getting-started/jellyfin/Jellyfin-plugin-catalog-repositories.jpg)

3. Click the **Plus icon** and add a new Repository. 
    - **Repository Name:** ACdb.tv
    - **Repository URL:** 
    ```
    https://raw.githubusercontent.com/jonjonsson/plugin.jellyfin.acdb.manifest/main/manifest.json
    ```
    - ![](https://acdb.tv/static/img/help/getting-started/jellyfin/Jellyfin-plugin-catalog-repositories-manifest.jpg)

3. Go back to Catalogue and you'll see the ACdb.tv plugin in the **General** section.
    - ![](https://acdb.tv/static/img/help/getting-started/jellyfin/Jellyfin-plugin-catalog-acdb.jpg)

4. After install, **restart Jellyfin**.


## Adding your first collection

1. After restarting Jellyfin, head to ACdb.tv plugin settings.
2. Inside the plugin, create a new account with one click. You'll be assigned a temporary username. 
3. Click on "See All Collections on ACdb.tv". The button will automatically log you in on ACdb.tv with your new account.
4. Find a collection you like, click "Add to Jellyfin", and it will sync to your server when it synchronizes next. You can click "Synchronize Now" in the plugin to immediately sync." 
5. To get 3 collection for free, go to the account page on ACdb.tv to link your account with Patreon and join the free tier. Only an email address is required. 
6. Now you can log in to ACdb.tv with you Patreon anytime to manage your collections. 

## Features

- **Free to Start:**  
  Get started with 3 collections for free. [Upgrade](https://www.patreon.com/c/acdbtv) from $2 a month.

- **Always Up to Date:**  
  Collections update automatically every couple of hours.

- **Collection Posters:**  
  Browse an ever-growing gallery of collection posters. Upload your own designs and generations!

- **See What You’re Missing:**  
  Compare your Jellyfin library with ACdb collections and see exactly what you’re missing (Supporters Only).

- **Seasonal Collections:**  
  Show horror in October, holiday films in December, and more. (Supporters Only)

- **MDBList Integration:**  
  Build your own lists or import from Trakt/IMDb, then turn them into collections on ACdb.tv. Mix TV and movies, combine lists, and more.

- **... and much more. Check out [ACdb.tv](https://acdb.tv)**

## Community & Support

- For help, questions, or feedback, [ACdb.tv Contact Page](https://acdb.tv/contact)
- Join us on [Discord](https://discord.gg/9kWgmGwg5e), for all of the above. New collections and posters are announced there as well.

## Versions

**2.2.0.0** — First version that includes Jellyfin support.

## License

This plugin is licensed under the GPLv3.