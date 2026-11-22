/* eslint-disable */
import * as types from './graphql';



/**
 * Map of all GraphQL operations in the project.
 *
 * This map has several performance disadvantages:
 * 1. It is not tree-shakeable, so it will include all operations in the project.
 * 2. It is not minifiable, so the string of a GraphQL query will be multiple times inside the bundle.
 * 3. It does not support dead code elimination, so it will add unused operations.
 *
 * Therefore it is highly recommended to use the babel or swc plugin for production.
 * Learn more about it here: https://the-guild.dev/graphql/codegen/plugins/presets/preset-client#reducing-bundle-size
 */
type Documents = {
    "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n": typeof types.ViewerDocument,
    "\n  query CampaignEditor($id: UUID!) {\n    campaign(id: $id) {\n      id\n      name\n      status\n      contactListId\n      sendEmail\n      sendSms\n      sendPost\n      emailSubject\n      emailBody\n      smsText\n      letterBody\n      landingTitle\n      landingBody\n      startedAtUtc\n      finishedAtUtc\n      lettersReady\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n      }\n    }\n  }\n": typeof types.CampaignEditorDocument,
    "\n  query CampaignMessages($id: UUID!, $after: String) {\n    campaignMessages(campaignId: $id, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        channel\n        status\n        token\n        sentAtUtc\n        error\n        contact {\n          id\n          name\n          email\n          phone\n        }\n      }\n    }\n  }\n": typeof types.CampaignMessagesDocument,
    "\n  mutation SaveCampaign($input: UpdateCampaignInput!) {\n    updateCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.SaveCampaignDocument,
    "\n  mutation StartCampaign($input: StartCampaignInput!) {\n    startCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.StartCampaignDocument,
    "\n  query Campaigns {\n    campaigns(first: 100) {\n      nodes {\n        id\n        name\n        status\n        sendEmail\n        sendSms\n        sendPost\n        createdAtUtc\n        startedAtUtc\n      }\n    }\n  }\n": typeof types.CampaignsDocument,
    "\n  mutation CreateCampaign($input: CreateCampaignInput!) {\n    createCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.CreateCampaignDocument,
    "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n": typeof types.ContactDetailDocument,
    "\n  query Contacts($filter: ContactFilterInput, $after: String) {\n    contacts(filter: $filter, first: 50, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        name\n        email\n        phone\n        city\n        obtainedOn\n        emailOptOut\n        smsOptOut\n      }\n    }\n  }\n": typeof types.ContactsDocument,
    "\n  query ContactsPageData {\n    categories {\n      id\n      name\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n      }\n    }\n  }\n": typeof types.ContactsPageDataDocument,
    "\n  mutation CreateListForSelection($input: CreateContactListInput!) {\n    createContactList(input: $input) {\n      contactList {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.CreateListForSelectionDocument,
    "\n  mutation AddToList($input: AddToContactListInput!) {\n    addToContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.AddToListDocument,
    "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n": typeof types.DocumentsDocument,
    "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RequestDocumentUploadDocument,
    "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.ConfirmDocumentUploadDocument,
    "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RenameDocumentDocument,
    "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.ReplaceDocumentContentDocument,
    "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.DeleteDocumentDocument,
    "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n": typeof types.ImportCategoriesDocument,
    "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.CreateImportCategoryDocument,
    "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RequestCsvUploadDocument,
    "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.ConfirmCsvUploadDocument,
    "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.PreviewImportDocument,
    "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.StartImportDocument,
    "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n": typeof types.ImportBatchesDocument,
    "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n": typeof types.ImportErrorsDocument,
    "\n  mutation CreateListFromBatch($input: CreateContactListFromBatchInput!) {\n    createContactListFromBatch(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.CreateListFromBatchDocument,
    "\n  query ContactLists {\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n        createdAtUtc\n        createdBy\n      }\n    }\n  }\n": typeof types.ContactListsDocument,
    "\n  query ContactListMembers($listId: UUID!, $after: String) {\n    contactListMembers(listId: $listId, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        addedAtUtc\n        contact {\n          id\n          name\n          email\n          phone\n          city\n        }\n      }\n    }\n  }\n": typeof types.ContactListMembersDocument,
    "\n  mutation RenameList($input: RenameContactListInput!) {\n    renameContactList(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.RenameListDocument,
    "\n  mutation DeleteList($input: DeleteContactListInput!) {\n    deleteContactList(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.DeleteListDocument,
    "\n  mutation RemoveFromList($input: RemoveFromContactListInput!) {\n    removeFromContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.RemoveFromListDocument,
    "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n": typeof types.QueuesDocument,
    "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n": typeof types.PurgeQueueDocument,
};
const documents: Documents = {
    "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n": types.ViewerDocument,
    "\n  query CampaignEditor($id: UUID!) {\n    campaign(id: $id) {\n      id\n      name\n      status\n      contactListId\n      sendEmail\n      sendSms\n      sendPost\n      emailSubject\n      emailBody\n      smsText\n      letterBody\n      landingTitle\n      landingBody\n      startedAtUtc\n      finishedAtUtc\n      lettersReady\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n      }\n    }\n  }\n": types.CampaignEditorDocument,
    "\n  query CampaignMessages($id: UUID!, $after: String) {\n    campaignMessages(campaignId: $id, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        channel\n        status\n        token\n        sentAtUtc\n        error\n        contact {\n          id\n          name\n          email\n          phone\n        }\n      }\n    }\n  }\n": types.CampaignMessagesDocument,
    "\n  mutation SaveCampaign($input: UpdateCampaignInput!) {\n    updateCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.SaveCampaignDocument,
    "\n  mutation StartCampaign($input: StartCampaignInput!) {\n    startCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.StartCampaignDocument,
    "\n  query Campaigns {\n    campaigns(first: 100) {\n      nodes {\n        id\n        name\n        status\n        sendEmail\n        sendSms\n        sendPost\n        createdAtUtc\n        startedAtUtc\n      }\n    }\n  }\n": types.CampaignsDocument,
    "\n  mutation CreateCampaign($input: CreateCampaignInput!) {\n    createCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.CreateCampaignDocument,
    "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n": types.ContactDetailDocument,
    "\n  query Contacts($filter: ContactFilterInput, $after: String) {\n    contacts(filter: $filter, first: 50, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        name\n        email\n        phone\n        city\n        obtainedOn\n        emailOptOut\n        smsOptOut\n      }\n    }\n  }\n": types.ContactsDocument,
    "\n  query ContactsPageData {\n    categories {\n      id\n      name\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n      }\n    }\n  }\n": types.ContactsPageDataDocument,
    "\n  mutation CreateListForSelection($input: CreateContactListInput!) {\n    createContactList(input: $input) {\n      contactList {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.CreateListForSelectionDocument,
    "\n  mutation AddToList($input: AddToContactListInput!) {\n    addToContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.AddToListDocument,
    "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n": types.DocumentsDocument,
    "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RequestDocumentUploadDocument,
    "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.ConfirmDocumentUploadDocument,
    "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RenameDocumentDocument,
    "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.ReplaceDocumentContentDocument,
    "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.DeleteDocumentDocument,
    "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n": types.ImportCategoriesDocument,
    "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.CreateImportCategoryDocument,
    "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RequestCsvUploadDocument,
    "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.ConfirmCsvUploadDocument,
    "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.PreviewImportDocument,
    "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.StartImportDocument,
    "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n": types.ImportBatchesDocument,
    "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n": types.ImportErrorsDocument,
    "\n  mutation CreateListFromBatch($input: CreateContactListFromBatchInput!) {\n    createContactListFromBatch(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.CreateListFromBatchDocument,
    "\n  query ContactLists {\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n        createdAtUtc\n        createdBy\n      }\n    }\n  }\n": types.ContactListsDocument,
    "\n  query ContactListMembers($listId: UUID!, $after: String) {\n    contactListMembers(listId: $listId, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        addedAtUtc\n        contact {\n          id\n          name\n          email\n          phone\n          city\n        }\n      }\n    }\n  }\n": types.ContactListMembersDocument,
    "\n  mutation RenameList($input: RenameContactListInput!) {\n    renameContactList(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.RenameListDocument,
    "\n  mutation DeleteList($input: DeleteContactListInput!) {\n    deleteContactList(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.DeleteListDocument,
    "\n  mutation RemoveFromList($input: RemoveFromContactListInput!) {\n    removeFromContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.RemoveFromListDocument,
    "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n": types.QueuesDocument,
    "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n": types.PurgeQueueDocument,
};

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n"): typeof import('./graphql').ViewerDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query CampaignEditor($id: UUID!) {\n    campaign(id: $id) {\n      id\n      name\n      status\n      contactListId\n      sendEmail\n      sendSms\n      sendPost\n      emailSubject\n      emailBody\n      smsText\n      letterBody\n      landingTitle\n      landingBody\n      startedAtUtc\n      finishedAtUtc\n      lettersReady\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n      }\n    }\n  }\n"): typeof import('./graphql').CampaignEditorDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query CampaignMessages($id: UUID!, $after: String) {\n    campaignMessages(campaignId: $id, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        channel\n        status\n        token\n        sentAtUtc\n        error\n        contact {\n          id\n          name\n          email\n          phone\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CampaignMessagesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation SaveCampaign($input: UpdateCampaignInput!) {\n    updateCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').SaveCampaignDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation StartCampaign($input: StartCampaignInput!) {\n    startCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').StartCampaignDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Campaigns {\n    campaigns(first: 100) {\n      nodes {\n        id\n        name\n        status\n        sendEmail\n        sendSms\n        sendPost\n        createdAtUtc\n        startedAtUtc\n      }\n    }\n  }\n"): typeof import('./graphql').CampaignsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation CreateCampaign($input: CreateCampaignInput!) {\n    createCampaign(input: $input) {\n      campaign {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CreateCampaignDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ContactDetailDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Contacts($filter: ContactFilterInput, $after: String) {\n    contacts(filter: $filter, first: 50, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        id\n        name\n        email\n        phone\n        city\n        obtainedOn\n        emailOptOut\n        smsOptOut\n      }\n    }\n  }\n"): typeof import('./graphql').ContactsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ContactsPageData {\n    categories {\n      id\n      name\n    }\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n      }\n    }\n  }\n"): typeof import('./graphql').ContactsPageDataDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation CreateListForSelection($input: CreateContactListInput!) {\n    createContactList(input: $input) {\n      contactList {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CreateListForSelectionDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation AddToList($input: AddToContactListInput!) {\n    addToContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').AddToListDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n"): typeof import('./graphql').DocumentsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RequestDocumentUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ConfirmDocumentUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RenameDocumentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ReplaceDocumentContentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').DeleteDocumentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n"): typeof import('./graphql').ImportCategoriesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CreateImportCategoryDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RequestCsvUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ConfirmCsvUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').PreviewImportDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').StartImportDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ImportBatchesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ImportErrorsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation CreateListFromBatch($input: CreateContactListFromBatchInput!) {\n    createContactListFromBatch(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CreateListFromBatchDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ContactLists {\n    contactLists(first: 100) {\n      nodes {\n        id\n        name\n        memberCount\n        createdAtUtc\n        createdBy\n      }\n    }\n  }\n"): typeof import('./graphql').ContactListsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ContactListMembers($listId: UUID!, $after: String) {\n    contactListMembers(listId: $listId, first: 100, after: $after) {\n      totalCount\n      pageInfo {\n        hasNextPage\n        endCursor\n      }\n      nodes {\n        addedAtUtc\n        contact {\n          id\n          name\n          email\n          phone\n          city\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ContactListMembersDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RenameList($input: RenameContactListInput!) {\n    renameContactList(input: $input) {\n      contactList {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RenameListDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation DeleteList($input: DeleteContactListInput!) {\n    deleteContactList(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').DeleteListDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RemoveFromList($input: RemoveFromContactListInput!) {\n    removeFromContactList(input: $input) {\n      count\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RemoveFromListDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n"): typeof import('./graphql').QueuesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n"): typeof import('./graphql').PurgeQueueDocument;


export function graphql(source: string) {
  return (documents as any)[source] ?? {};
}
