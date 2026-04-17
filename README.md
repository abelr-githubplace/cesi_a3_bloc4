# Projet EasySave de chez ProSoft

## Sujet

### Consignes de travail

**Calendrier**\
Durant ce projet fil rouge, vous allez vivre en manière accélérée le développement de 3 versions du logiciel de sauvegarde EasySave.

**Livrable 0 et Livrable 1 (EasySave version 1.0) :**

- 1er  jour : Lancement du projet et Cahier des charges version 1.0
- 3 eme jour : Création d'un environnement de travail et envoi des accès au tuteur (sera évalué sur l'ensemble du projet)
- Veille Livrable 1 : Livraison des diagrammes UML
- Jour Livrable 1 : Réception du livrable 1 (version 1.0 de EasySave) et documentations associées.

**Livrable 2 (EasySave versions 2.0 et 1.1) :** // non évalué

- Lendemain Livrable 1 : Mise à disposition du Cahier des charges de la version 2 et de la version 1.1
- Veille  Livrable 2 : Livraison des diagrammes UML
- Jour Livrable 2 : Réception du livrable 2

**Livrable 3 (EasySave version 3.0) :**

- Lendemain Livrable 2 : Mise à disposition du Cahier des charges de la version 3
- Avant-veille soutenance : Livraison des diagrammes UML
- Veille soutenance : Réception du livrable 3
- Jour soutenance : Soutenance du projet.

**Présentation de Prosoft**\
Votre équipe vient d'intégrer l'éditeur de logiciels ProSoft. Sous la responsabilité du DSI, vous aurez la responsabilité de gérer le projet “EasySave” qui consiste à développer un logiciel de sauvegarde.

Comme tout logiciel de la Suite ProSoft, le logiciel s'intégrera à la politique tarifaire.

- Prix unitaire : 200 €HT
- Contrat de maintenance annuel 5/7 8-17h (mises à jour incluses): 12% prix d'achat (Contrat annuel à tacite reconduction avec revalorisation basée sur l'indice SYNTEC)

Lors de ce projet, votre équipe devra assurer le développement, la gestion des versions majeures et mineures, mais aussi les documentations

- pour les utilisateurs : manuel d'utilisation (sur une page)
- pour le support client : Informations nécessaires pour le support technique (Emplacement par défaut du logiciel, Configuration minimale, Emplacement des fichiers de configuration...)

Pour garantir une reprise de votre travail par d'autres équipes, la direction vous impose de travailler dans le respect des contraintes suivantes :

- Outils et méthodes (à valider avec votre responsable)
  - Visual Studio 2022 ou supérieure
  - GITHub
  - Editeur UML : Nous préconisations l'utilisation de ArgoUML\
      « Tous vos documents et l'ensemble des codes doivent être gérés avec ces outils. »\
      « Votre responsable (tuteur ou pilote) doit être invité sur votre GIT pour pouvoir suivre vos développements »
- Langage, FrameWork
  - Langage C#
  - Bibliothèque .Net 8.0
- Lisibilité et maintenabilité du code :
  - L'ensemble des documents, lignes de code et commentaires doit être exploitable par les filiales anglophones.
  - Le nombre de lignes de code dans une fonction doit être raisonnable.
  - La redondance des lignes de code est à proscrire (une vigilance particulière sera portée sur les copier-coller).
  - Respect des conventions de nommage
- Autres :
  - La documentation utilisateur doit tenir en une seule page
  - Release note obligatoire

Vous devez conduire ce projet de manière à réduire les coûts de développement des futures versions et surtout à être capable de réagir rapidement à la remontée éventuelle d'un dysfonctionnement.

- Gestion des versions
- Limiter au maximum les lignes de code dupliquées

Le logiciel devant être distribué chez les clients, il est impératif de soigner les IHM.



### Livrables attendus

Votre équipe doit installer un environnement de travail respectant les contraintes imposées par ProSoft.

Le bon usage de l'environnement de travail et des contraintes imposées par la direction seront évalués tout au long du projet.

Une vigilance particulière sera portée sur :

- La gestion de GIT (versioning, suivi des modifications, travail en équipe,...)
- Les diagrammes UML à rendre 24 heures avant chaque livrable (La veille)
- La qualité du code (absence de redondance dans les lignes de code)
- L'architecture du code

## Livrable 1

### Version 1.0

**Description du livrable 1 : EasySave version 1.0**

- Le cahier des charges de la première version du logiciel est le suivant :
- Le logiciel est une application Console utilisant .Net.
- Le logiciel doit permettre de créer jusqu'à 5 travaux de sauvegarde
- Un travail de sauvegarde est défini par :
  - Un nom de sauvegarde
  - Un répertoire source
  - Un répertoire cible
- Un type (sauvegarde complète , sauvegarde différentielle)
- Le logiciel doit être utilisable à minima par des utilisateurs anglophones et Francophones
- L'utilisateur peut demander l'exécution d'un des travaux de sauvegarde ou l'exécution séquentielle de l'ensemble des travaux.
- Le fichier exécutable du programme peut être executé  sur le terminal  par une ligne de commande
  - exemple 1 : « EasySave.exe 1-3 » pour exécuter automatiquement les sauvegardes 1 à 3
  - exemple 2 : « EasySave.exe 1 ;3 »  pour exécuter automatiquement les sauvegardes 1 et 3
- Les répertoires (sources et cibles) pourront être sur :
  - Des disques locaux
  - Des disques Externes
  - Des Lecteurs réseaux
- Tous les éléments d'un répertoire source (fichiers et sous-répertoires ) doivent être sauvegardé.
- Fichier Log journalier :
    
    Le logiciel doit écrire en temps réel dans un fichier log journalier toutes les actions réalisées durant les sauvegardes (transfert d'un fichier, création d'un répertoire, ...).

    Les informations minimales attendues sont :

  - Horodatage
  - Nom de sauvegarde
  - Adresse complète du fichier Source (format UNC)
  - Adresse complète du fichier de destination (format UNC)
  - Taille du fichier
  - Temps de transfert du fichier en ms (négatif si erreur)
  - Exemple de contenu: 2020-12-17.json [json, 991 o]

    L'écriture des informations dans un fichier Log journalier est une fonctionnalité qui servira à d'autres projets. Il vous est demandé de développer cette fonctionnalité dans une Dynamic Link Library nommée « EasyLog .dll» avec un choix réfléchi  de ce projet dans GIT. Toutes les évolutions de cette librairie doivent rester compatibles avec la version 1.0 du logiciel.

- Ficher Etat temps réel : Le logiciel doit enregistrer en temps réel, dans un fichier unique, l'état d'avancement des travaux de sauvegarde et l'action en cours. Les informations à enregistrer pour chaque travail de sauvegarde sont à minima:
  - Appellation du travail de sauvegarde
  - Horodatage de la dernière action
  - Etat du travail de Sauvegarde (ex : Actif, Non Actif...)
  - Si le travail est actif :
    - Le nombre total de fichiers éligibles
      - La taille des fichiers à transférer
      - La progression
      - Nombre de fichiers restants
      - Taille des fichiers restants
      - Adresse complète du fichier Source en cours de sauvegarde
      - Adresse complète du fichier de destination

  Exemple  : state.json [json, 762 o]

- Les emplacements des deux fichiers (log journalier et état temps réel) devront être étudiés pour fonctionner sur les serveurs de nos clients. De ce fait, les emplacements du type « c:\temp\ » sont à proscrire.
- Les fichiers (log journalier et état) et les éventuels fichiers de configuration seront au format JSON. Pour permettre une lecture rapide via Notepad, il est nécessaire de mettre des retours à la ligne entre les éléments JSON. Une pagination serait un plus.

**Remarque importante :** si le logiciel donne satisfaction, la direction vous demandera de développer une version 2.0 utilisant une interface graphique (basée sur l'architecture MVVM)

## Livrable 2

## Livrable 3