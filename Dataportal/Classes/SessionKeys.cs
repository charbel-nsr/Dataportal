namespace Dataportal.Classes
{
    /* Session keys that keep track of the dataset creation wizard so a user can
     * resume the flow and so completed stages route back to the résumé page.
     *  CreationMetadonneeId
     *  Identifier of the Metadonnee currently being assembled. 
     *  It is stored after step 2 finishes so that any subsequent step can redirect back to the résumé if the user leaves the wizard.
     *  
     *  CreationNextStep
     *  Number of the next creation step the user should see. 
     *  It starts at 3 once the Données are created, increments as each optional step is completed or skipped, 
     *  and is cleared when the résumé/details page loads.
     */ 
    public static class SessionKeys
    {
        public const string CreationMetadonneeId = "CreationMetadonneeId";
        public const string CreationNextStep = "CreationNextStep";
        public const string ModificationMetadonneeId = "ModificationMetadonneeId";
        public const string ModificationNextStep = "ModificationNextStep";
    }
}